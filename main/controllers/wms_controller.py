import numpy as np
import json
from json import JSONEncoder
import os
from datetime import datetime
from main.entities.post_map import TileClass

class NumpyArrayEncoder(JSONEncoder):
    def default(self, obj):
        if isinstance(obj, np.ndarray):
            return obj.tolist()
        return JSONEncoder.default(self, obj)

class GraphMap:
    def __init__(self, map_controller, save_dir):
        self.map_controller = map_controller
        self.map_graph = [] #list of sets
        self.shortest_pathes = []
        self.next_tiles = []
        self.file_path = os.path.join(os.path.normpath(save_dir), "graphmap.json")
        self.number_nodes = self.map_controller.size
        
        if os.path.exists(os.path.normpath(self.file_path)):
            self.load_from_file(self.file_path)
            if self.map_graph == [] or self.wanted_tiles == []:
                self.init_map_graph()
                self.save_state()
            if self.shortest_pathes == [] or self.next_tiles == []:
                self.count_shortest_pathes()
                self.save_state()
        else:
            self.init_map_graph()
            self.count_shortest_pathes()   
            self.save_state()
       
    def load_from_file(self, file_path):
        with open(file_path, "r") as file:
            f = json.load(file)
            self.map_graph = f.get("map_graph", [])
            self.shortest_pathes = np.asarray(f.get("shortest_pathes",[]))
            self.next_tiles = np.asarray(f.get("next_tiles", []))
            self.wanted_tiles = f.get("wanted_tiles", [])
        print(f"{datetime.now().strftime('%H:%M:%S')}: wms controller successfully loaded")
    def save_state(self):
        with open(self.file_path, "w") as f:
            json.dump({"map_graph" : self.map_graph, 
                       "shortest_pathes" : self.shortest_pathes, 
                       "next_tiles" : self.next_tiles, 
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
        
    def build_in_width(self, start_id, visited, current_level, depth = 1):
        while len(current_level) > 0:
            next_level = set()
            for node in current_level:
                for neigbour in self.map_graph[node]:
                    if not visited[neigbour]:
                        self.shortest_pathes[start_id][neigbour] = depth
                        self.next_tiles[start_id][neigbour] = node
                        visited[neigbour] = True
                        next_level.add(neigbour)
            depth+=1
            current_level = next_level
        
    def count_pathes_from(self, tile_id):
        visited = [False]*self.number_nodes
        visited[tile_id] = True
        current_level = {tile_id}
        self.build_in_width(tile_id, visited, current_level, depth=1)
        
    def count_pathes_to(self, tile_id, reversed_map_graph):
        visited = [False]*self.number_nodes
        visited[tile_id] = True      
        current_level = {tile_id}
        depth = 1
        while len(current_level) > 0:
            next_level = set()
            for node in current_level:
                for neigbour in reversed_map_graph[node]:
                    if not visited[neigbour]:
                        self.shortest_pathes[neigbour][tile_id] = depth
                        self.next_tiles[neigbour][tile_id] = node
                        visited[neigbour] = True
                        next_level.add(neigbour)
            depth+=1
            current_level = next_level
        
    def count_shortest_pathes(self):
        # with floid algorithm let's count shortest pathes (maybe i'll even save that into a seperate file for speed)
        number_tiles = len(self.map_graph)
        self.shortest_pathes = np.array([[-1]*number_tiles for _ in range(number_tiles)])
        self.next_tiles = np.array([[-1]*number_tiles for _ in range(number_tiles)])
        # presetup the shortest pathes with edge
        for i in range(number_tiles):
            self.shortest_pathes[i, i] = 0
            self.next_tiles[i, i] = i
            
        reversed_map_graph = [[] for i in range(self.number_nodes)]
        for node in range(self.number_nodes):
            for neig in self.map_graph[node]:
                reversed_map_graph[neig].append(node)
        
        num_worked_on = 0
        d = len(self.wanted_tiles)//10
        for tile in self.wanted_tiles:
            self.count_pathes_from(tile)
            self.count_pathes_to(tile, reversed_map_graph)
            num_worked_on += 1
            if num_worked_on%d == 0:
                print(f"finished sorting tiles on {num_worked_on/d*10}%")
        del reversed_map_graph
        self.save_state()
    def tile2id(self, tile):
        return tile.y*self.map_controller.tile_width + tile.x
    def id2coords(self, tile_id):
        return tile_id%self.map_controller.tile_width, tile_id//self.map_controller.tile_width
    def shortest_path_length(self, tile_start, tile_end):
        id_start = self.tile2id(tile_start)
        id_end = self.tile2id(tile_end)
        return self.shortest_pathes[id_start, id_end]
    
    def get_shortest_path_(self, id_start, id_end):
        if self.next_tiles[id_start, id_end] == -1:
            return None # there is no path
        else:
            id_cur = self.next_tiles[id_start, id_end]
            path = []#[0 for _ in range(self.shortest_pathes[id_start, id_end])]
            while id_cur != id_end:
                path.append((0, self.id2coords(id_cur)))
                id_cur = self.next_tiles[id_cur, id_end]
            path.append((0, self.id2coords(id_cur)))
            print(f"controller built path from {self.id2coords(id_start)} to {self.id2coords(id_end)}: {path}")
            return path  
        
    def get_shortest_path(self, tile_start, tile_end):
        """returns path already in format [(0, (x,y)), ...] or None if path doesn't exist"""
        id_start = self.tile2id(tile_start)
        id_end = self.tile2id(tile_end)
        return self.get_shortest_path_(id_start, id_end)

class WMS_communicator:
    SYSTEM_TYPES = ['SERVER', 'FROM_FILE', 'DEFINE']
    def __init__(self, map_controller, input_type = 'DEFINE', fpath = None, server = None, logpath = None, **kwargs):
        assert input_type in WMS_communicator.SYSTEM_TYPES
        self.input_type = input_type
        self.fpath = fpath
        self.server = server
        self.map_controller = map_controller
        
        self.graph_map = None
        if self.input_type == 'DEFINE':
            self.graph_map = GraphMap(self.map_controller, logpath)
                        
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
        end_tile = min([dest.send_from_tile for dest in self.map_controller.destinations[destination]], lambda p_end_tile: self.graph_map.shortest_path_length(start_tile, p_end_tile))
        
        rec_pkg = (1, (receiver_id, pkg_id))
        del_pkg = (2, (receiver_id, pkg_id))
        return {'robot_id' : robot.robot_id, 'receiver_id' : receiver_id, 'command' : [rec_pkg,*self.build_path_between_tiles(start_tile, end_tile), del_pkg]}
    
        """
        # for robot example
        robot_id = 'rob_0'
        cur_x, cur_y = self.map_controller.get_robot_coordinates_by_id(robot_id)
        rec_pkg = (1, (receiver_id, pkg_id))
        move_1 = (0, (cur_x + 1, cur_y))
        move_2 = (0, (cur_x, cur_y))
        del_pkg = (2, (receiver_id, pkg_id))
        return {'robot_id' : robot_id, 'receiver_id' : None, 'command' : [rec_pkg,move_1, move_2, del_pkg]}
        """
        
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