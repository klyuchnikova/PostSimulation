import numpy as np
import json
from json import JSONEncoder
import os
from datetime import datetime
from main.entities.post_map import TileClass

def jsonKeys2int(x):
    if isinstance(x, dict):
        try:
            return {int(k):v for k,v in x.items()}
        except:
            return x
    return x

class NumpyArrayEncoder(JSONEncoder):
    def default(self, obj):
        if isinstance(obj, np.ndarray):
            return obj.tolist()
        return JSONEncoder.default(self, obj)

class GraphMap:
    def __init__(self, map_controller, save_dir = None):
        self.map_controller = map_controller
        self.map_graph = [] #list of sets
        self.shortest_pathes = []
        self.next_tiles = dict()
        self.prev_tiles = dict()
        if save_dir is None:
            # load default
            self.file_path = os.path.join(os.path.dirname(os.path.abspath(__file__)), os.path.normpath("..\\..\\data\\simulation_data\\default\\graphmap.json"))
        else:
            self.file_path = os.path.join(os.path.normpath(save_dir), "graphmap.json")
        self.number_nodes = self.map_controller.size
        
        if os.path.exists(os.path.normpath(self.file_path)):
            self.load_from_file(self.file_path)
            if len(self.map_graph) == 0 or len(self.wanted_tiles) == 0:
                self.init_map_graph()
            if len(self.shortest_pathes) == 0 or len(self.next_tiles) == 0:
                self.count_shortest_pathes()
        else:
            self.init_map_graph()
            self.count_shortest_pathes()  
            self.save_state()
       
    def load_from_file(self, file_path):
        with open(file_path, "r") as file:
            f = json.load(file, object_hook=jsonKeys2int)
            self.map_graph = f.get("map_graph", [])
            self.shortest_pathes = np.asarray(f.get("shortest_pathes",[]))
            self.next_tiles = f.get("next_tiles", dict())
            self.prev_tiles = f.get("prev_tiles", dict())
            self.wanted_tiles = f.get("wanted_tiles", [])
        print(f"{datetime.now().strftime('%H:%M:%S')}: wms controller successfully loaded")
    def save_state(self):
        with open(self.file_path, "w") as f:
            json.dump({"map_graph" : self.map_graph, 
                       "shortest_pathes" : self.shortest_pathes, 
                       "next_tiles" : self.next_tiles, 
                       "prev_tiles" : self.prev_tiles,
                       "wanted_tiles" : self.wanted_tiles}, f, cls=NumpyArrayEncoder)
        
    def init_map_graph(self):
        # vector of ordered sets - classic graph where vertice ids are tile.y*width + tile.x
        # simply to make it more efficient
        self.map_graph = [[] for _ in range(self.map_controller.size)]
        self.wanted_tiles = [] # chargers, sorting tiles, sending areas and beginning of queues
        for x in range(self.map_controller.tile_width):
            for y in range(self.map_controller.tile_height):
                id_ = x + y*self.map_controller.tile_width
                for d in self.map_controller.get(x,y).available_directions_:
                    if d == 0:
                        self.map_graph[id_].append(id_ - self.map_controller.tile_width)
                    elif d == 1:
                        self.map_graph[id_].append(id_ + 1)
                    elif d == 2:
                        self.map_graph[id_].append(id_ + self.map_controller.tile_width)
                    else:
                        self.map_graph[id_].append(id_ - 1)
                tile_class = self.map_controller.get(x,y).tile_class               
                if tile_class == TileClass.CHARGE_TILE:
                    self.wanted_tiles.append(id_)
                elif tile_class == TileClass.STATION_TILE:
                    self.wanted_tiles.append(id_)
                elif tile_class == TileClass.SENDING_TILE:
                    self.wanted_tiles.append(id_)
                elif tile_class == TileClass.QUEUE_TILE and self.map_controller.get(x, y).in_queue_order == 0:
                    self.wanted_tiles.append(id_)
        self.save_state()
        
    def count_pathes_from(self, tile_id):
        visited = [False]*self.number_nodes
        visited[tile_id] = True
        current_level = {tile_id}
        depth = 1
        prev_tiles = self.prev_tiles[tile_id]
        while len(current_level) > 0:
            next_level = set()
            for node in current_level:
                for neigbour in self.map_graph[node]:
                    if not visited[neigbour]:
                        self.shortest_pathes[tile_id][neigbour] = depth
                        prev_tiles[neigbour] = node
                        visited[neigbour] = True
                        next_level.add(neigbour)
            depth+=1
            current_level = next_level
        
    def count_pathes_to(self, tile_id, reversed_map_graph):
        visited = [False]*self.number_nodes
        visited[tile_id] = True      
        current_level = {tile_id}
        depth = 1
        next_tiles = self.next_tiles[tile_id]
        while len(current_level) > 0:
            next_level = set()
            for node in current_level:
                for neigbour in reversed_map_graph[node]:
                    if not visited[neigbour]:
                        self.shortest_pathes[neigbour][tile_id] = depth
                        next_tiles[neigbour] = node
                        visited[neigbour] = True
                        next_level.add(neigbour)
            depth+=1
            current_level = next_level
        
    def get_reversed_map_graph(self):
        reversed_map_graph = [[] for i in range(self.number_nodes)]
        for node in range(self.number_nodes):
            for neig in self.map_graph[node]:
                reversed_map_graph[neig].append(node) 
        return reversed_map_graph
    
    def count_pathes_to_(self, tile_id, reversed_map_graph):
        next_tiles = [-1]*self.number_nodes
        shortest_pathes = [-1]*self.number_nodes
        visited = [False]*self.number_nodes
        visited[tile_id] = True  
        shortest_pathes[tile_id] = 0
        current_level = {tile_id}
        depth = 1
        while len(current_level) > 0:
            next_level = set()
            for node in current_level:
                for neigbour in reversed_map_graph[node]:
                    if not visited[neigbour]:
                        shortest_pathes[neigbour] = depth
                        next_tiles[neigbour] = node
                        visited[neigbour] = True
                        next_level.add(neigbour)
            depth+=1
            current_level = next_level   
        return shortest_pathes, next_tiles
        
    def count_shortest_pathes(self):
        # with floid algorithm let's count shortest pathes (maybe i'll even save that into a seperate file for speed)
        number_tiles = len(self.map_graph)
        self.shortest_pathes = np.array([[-1]*number_tiles for _ in range(number_tiles)])
        self.next_tiles = dict([(key, [-1]*number_tiles) for key in self.wanted_tiles])
        self.prev_tiles = dict([(key, [-1]*number_tiles) for key in self.wanted_tiles])
        # presetup the shortest pathes with edge
        for i in range(number_tiles):
            self.shortest_pathes[i, i] = 0
        for i in self.wanted_tiles:
            self.next_tiles[i][i] = i
            self.prev_tiles[i][i] = i
        reversed_map_graph = self.get_reversed_map_graph()
        num_worked_on = 0
        d = len(self.wanted_tiles)//10
        for tile in self.wanted_tiles:
            self.count_pathes_from(tile)
            self.count_pathes_to(tile, reversed_map_graph)
            num_worked_on += 1
            if num_worked_on%d == 0:
                print(f"finished sorting tiles on {num_worked_on/d*10}%")
        del reversed_map_graph
        
    def tile2id(self, tile):
        return tile.y*self.map_controller.tile_width + tile.x
    def id2coords(self, tile_id):
        return tile_id%self.map_controller.tile_width, tile_id//self.map_controller.tile_width
    def shortest_path_length(self, tile_start, tile_end):
        id_start = self.tile2id(tile_start)
        id_end = self.tile2id(tile_end)
        return self.shortest_pathes[id_start, id_end]
    
    def get_shortest_path_(self, id_start, id_end):
        max_n = self.shortest_pathes[id_start][id_end]
        if id_start in self.wanted_tiles:
            # then we move FROM id_start - therefore we look at row [id_start] and move backwards
            id_cur = id_end
            path = [0]*max_n #[0 for _ in range(self.shortest_pathes[id_start, id_end])]
            prev_tiles =  self.prev_tiles[id_start]
            for i in range(max_n-1, -1, -1):
                path[i] = (0, self.id2coords(id_cur))
                id_cur = prev_tiles[id_cur]
        elif id_end in self.wanted_tiles:
            # then we move TO id_end - therefore we take column [id_end] and move forward
            next_tiles = self.next_tiles[id_end]
            id_cur = next_tiles[id_start]
            path = [0]*max_n #[0 for _ in range(self.shortest_pathes[id_start, id_end])]
            for i in range(max_n):
                path[i] = (0, self.id2coords(id_cur))
                id_cur = next_tiles[id_cur]
        else:
            path = []
        #print(f"controller built path from {self.id2coords(id_start)} to {self.id2coords(id_end)}: {path}")
        return path  
        
    def get_shortest_path(self, tile_start, tile_end):
        """returns path already in format [(0, (x,y)), ...] or None if path doesn't exist"""
        id_start = self.tile2id(tile_start)
        id_end = self.tile2id(tile_end)
        return self.get_shortest_path_(id_start, id_end)

class WMS_communicator:
    SYSTEM_TYPES = ['SERVER', 'FROM_FILE', 'DEFINE']
    def __init__(self, map_controller, input_type = 'DEFINE', fpath = None, server = None, logpath = None, GRAPH_SAVE_MAP = None, **kwargs):
        assert input_type in WMS_communicator.SYSTEM_TYPES
        self.input_type = input_type
        self.fpath = fpath
        self.server = server
        self.map_controller = map_controller
        
        self.graph_map = None
        if self.input_type == 'DEFINE':
            self.graph_map = GraphMap(self.map_controller, GRAPH_SAVE_MAP)
                        
    def receive_message(self, event):
        print("nameless receive is called")
        pkg_id = event['id']
        receiver_id = event['conveyer_id']
        destination = event['destination']
        return self.build_path(None, receiver_id, pkg_id, destination)      
        
    def build_path(self, robot, receiver_id, pkg_id, destination):
        # path is built in an order-tile like manner
        # response is like {'robot_id' : None or robot id, 
        #                   'receiver_id' : None or receiver_id, 'command' : [# commad like (c_type, data) ]}
        # c_type : 0 - move | 1 - receive pckg      | 2 - deliver pckg | 3 - charge
        # data :  (x,y)     | (receiver_id, pkg_id) |      -//-        | (chrger_id, charge_time) 
        if robot is None:
            start_tile = self.map_controller.station_tiles[receiver_id]["tile"]
        else:
            start_tile = robot.position
        # choose the closest one from potential
        end_tile = min(self.map_controller.destinations[destination], key = lambda x: self.graph_map.shortest_path_length(start_tile, x.send_from_tile)).send_from_tile
        rec_pkg = (1, (receiver_id, pkg_id))
        del_pkg = (2, (receiver_id, pkg_id))
        return {'robot_id' : robot.robot_id, 'receiver_id' : receiver_id, 'command' : [rec_pkg,*self.build_path_between_tiles(start_tile, end_tile), del_pkg]}
        
    def build_path_between_tiles(self, tile_start, tile_end):
        return self.graph_map.get_shortest_path(tile_start, tile_end)
        
    def send_robot_to_queue(self, robot, queue):
        # first build path to first tile of queue to receiver, then build rest of path as queue order suggests
        start_tile = robot.position
        mid_x, mid_y = queue.path[0].x, queue.path[0].y
        commands = []
        commands.extend(self.build_path_between_tiles(tile_start=robot.position, tile_end=queue.path[0])) # from pos to start of the queue
        commands.extend([(0, (tile.x, tile.y)) for tile in queue.path[1:]])
        return {'robot_id' : robot.robot_id, 'receiver_id' : None, 'command' : commands}
        
    def send_robot_to_charge(self, robot):
        pass
   
from main.entities.post_map import PostMap 
import simpy
if __name__ == "__main__":
    map_file_path=r"E:\E\Copy\PyCharm\RoboPost\PostSimulation\data\simulation_data\sim_v0\map_v0.xml"
    map_ = PostMap(simpy.Environment(), map_file_path = map_file_path)
    grapg_map = GraphMap(map_, r'E:\E\Copy\PyCharm\RoboPost\PostSimulation\data\logs\sim_v0')
    #grapg_map.count_shortest_pathes()
    #grapg_map.save_state()