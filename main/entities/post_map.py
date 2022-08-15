import simpy 
import datetime
import random
from main.tools.loaders import load_map_configuration
from enum import Enum
import os, sys
import numpy as np
import matplotlib.pyplot as plt


class TileState(Enum):
    NULL_CELL = 0
    SHELF_CELL = 1
    E2W_PATH_CELL = 2
    W2E_PATH_CELL = 3
    S2N_PATH_CELL = 4
    N2S_PATH_CELL = 5
    E2W_S2N_PATH_CELL = 6
    E2W_N2S_PATH_CELL = 7
    W2E_S2N_PATH_CELL = 8
    W2E_N2S_PATH_CELL = 9
    E2W_W2E_PATH_CELL = 10
    N2S_S2N_PATH_CELL = 11
    E2W_W2E_N2S_PATH_CELL = 12
    E2W_W2E_S2N_PATH_CELL = 13
    N2S_S2N_E2W_PATH_CELL = 14
    N2S_S2N_W2E_PATH_CELL = 15
    OMNI_DIR_CELL = 16
    PICKSTATION_PICK_CELL = 17
    PICKSTATION_TURN_CELL = 18
    PICKSTATION_PATH_CELL = 19
    CHARGER_CELL = 20
    CHARGER_PI_CELL = 21
    BLOCKED_CELL = 22
    ENTRY_CELL = 23
    EXIT_CELL = 24
    ERROR = 25
    
TILE_TYPE_PICTURES = {
    TileState.NULL_CELL._value_ : "NULL_CELL.png",
    TileState.SHELF_CELL._value_ : "SHELF_CELL.png",
    TileState.E2W_PATH_CELL._value_ : "E2W_PATH_CELL.png",
    TileState.W2E_PATH_CELL._value_ : "W2E_PATH_CELL.png",
    TileState.S2N_PATH_CELL._value_ : "S2N_PATH_CELL.png",
    TileState.N2S_PATH_CELL._value_ : "N2S_PATH_CELL.png",
    TileState.E2W_S2N_PATH_CELL._value_ : "E2W_S2N_PATH_CELL.png",
    TileState.E2W_N2S_PATH_CELL._value_ : "E2W_N2S_PATH_CELL.png",
    TileState.W2E_S2N_PATH_CELL._value_ : "W2E_S2N_PATH_CELL.png",
    TileState.W2E_N2S_PATH_CELL._value_ : "W2E_N2S_PATH_CELL.png",
    TileState.E2W_W2E_PATH_CELL._value_ : "E2W_W2E_PATH_CELL.png",
    TileState.N2S_S2N_PATH_CELL._value_ : "N2S_S2N_PATH_CELL.png",
    TileState.E2W_W2E_N2S_PATH_CELL._value_ : "E2W_W2E_N2S_PATH_CELL.png",
    TileState.E2W_W2E_S2N_PATH_CELL._value_ : "E2W_W2E_S2N_PATH_CELL.png",
    TileState.N2S_S2N_E2W_PATH_CELL._value_ : "N2S_S2N_E2W_PATH_CELL.png",
    TileState.N2S_S2N_W2E_PATH_CELL._value_ : "N2S_S2N_W2E_PATH_CELL.png",
    TileState.OMNI_DIR_CELL._value_ : "OMNI_DIR_CELL.png",
    17 : "OMNI_DIR_CELL.png",
    18 : "OMNI_DIR_CELL.png",
    19 : "OMNI_DIR_CELL.png",
    20 : "OMNI_DIR_CELL.png",
    21  : "NULL_CELL.png",
    22  : "NULL_CELL.png",
    23  : "NULL_CELL.png",
    24 : "NULL_CELL.png",
    25  : "NULL_CELL.png"
}

def get_available_direction_list(tile_type):
    if tile_type._value_ == 1 or 16<=tile_type._value_<=20:
        return {0, 1, 2, 3}
    if tile_type._value_ in {0, 21, 22}:
        return set()
    directions = set()
    if "2N" in tile_type._name_:
        directions.add(0)    
    if "2E" in tile_type._name_:
        directions.add(1)
    if "2S" in tile_type._name_:
        directions.add(2)
    if "2W" in tile_type._name_:
        directions.add(3)
    return directions

class TileClass(Enum):
    PATH_TILE = 0
    STATION_TILE = 1
    CHARGE_TILE = 2
    SORTING_TILE = 3
    QUEUE_TILE = 4
    BLOCK = 5
    DESTINATION = 6
    SENDING_TILE = 7

class Tile:    
    def __init__(self, env, tile_id, tile_type_id, x, y):
        self.env = env
        self.tile_id = tile_id
        self.tile_type = TileState[TileState._member_names_[tile_type_id]]
        self.available_directions_ = get_available_direction_list(self.tile_type)
        self.x = x
        self.y = y
        if len(self.available_directions_) == 0:
            self.tile_class = TileClass.BLOCK
        else:
            self.tile_class = TileClass.PATH_TILE
        self.neighbours = [None]*4
        self.robot = None
        
        if 1<=self.tile_type._value_<=20:
            self.container = simpy.resources.container.Container(env, 1, init = 0)
        else:
            self.container = None
        
        self.connection_parameters = 1.
        self.receive_package_direction = None #if virtualstation then is a number from 1 to 4
        
    def init_move_in(self, robot):
        print(f"robot {robot.robot_id} starts in ({self.x}, {self.y})")
        self.robot = robot
        self.container = simpy.resources.container.Container(self.env, 1, init = 1)
    def init_move_out(self, robot):
        print(f"robot {robot.robot_id} is forcefully removed from ({self.x}, {self.y})")
        self.robot = None
        self.container = simpy.resources.container.Container(self.env, 1, init = 0)  
        
    def request_move_in(self, robot):
        if self.container:
            print("capacity & level: ", self.container.capacity, self.container.level)
            yield self.container.put(1)
            print("capacity & level: ", self.container.capacity, self.container.level)
            
        else:
            raise Exception("tile not movable!")
        self.robot = robot
    
    def request_move_out(self, robot):
        print(f"robot {robot.robot_id} requests to move out of ({self.x}, {self.y})")
        yield self.container.get(1)
        self.robot = None
        
    def direction_available_(self, direction = 0):
        # returns true if the markup allows moving in the given direction from this tile
        # 0 to 3 = N to W
        return self.container and (direction in available_directions_)
    
class QueueTile(Tile):
    def __init__(self, env, queue_controller, tile_id, reciever_tile, queue_size):
        self.env = env
        self.queue_controller = queue_controller
        self.tile_id = tile_id
        self.tile_type = reciever_tile.tile_type
        self.available_directions_ = reciever_tile.available_directions_
        self.x = reciever_tile.x
        self.y = reciever_tile.y
        self.tile_class = TileClass.SORTING_TILE
        self.neighbours = reciever_tile.neighbours
        self.robot = None # allways None
        self.queue_size = queue_size
        
        self.container = simpy.resources.container.Container(self.env, self.queue_size, init = 0)
        
        self.connection_parameters = 1.
        self.receive_package_direction = None
        
    def request_move_in(self, robot):
        if self.container:
            yield self.container.get(1)
            self.queue_controller.move_in_robot(robot, robot.position)
        else:
            raise Exception("tile not movable!")
    def init_move_in(self, robot):
        print(f"robot {robot.robot_id} is initially moved to receiver in ({self.x}, {self.y})")
        self.robot = robot
        self.container = simpy.resources.container.Container(self.env, self.queue_size, init = self.container.level + 1)    
        
class PostMap:
    def __init__(self, env, map_file_path = "..\\..\\data\\simulation_data\\map.xml"):
        self.env = env
        self.left_shift_ = 0
        self.up_shift_ = 0
        self.tile_type_map_ = np.array([[],])
        self.tile_map = [[],]
        # loading file and defining type map as well as some parameters
        self.load(map_file_path)
        
    def load(self, file_path):
        tree = load_map_configuration(file_path)
        # keys are ['mapcells', 'mapcells-attrs', 'stations', 'virtualstations', 'chargers', 'sortingareas', '#attributes', 'destinations', 'sendingareas']
        
        for attr_name_, attr_val_ in tree['#attributes'].items():
            self.__dict__[attr_name_] = float(attr_val_)
         
        self.tile_type_map_ = np.array([[int(x) for x in line.split(",") if x.strip()!=""] for line in tree['mapcells'].split("\n")])
        assert self.tile_type_map_.shape[::-1] == tuple(map(int, (tree['mapcells-attrs']['length'], tree['mapcells-attrs']['width'])))     
            
        min_x, min_y = self.tile_type_map_.shape
        max_x, max_y = 0, 0
        self.station_tiles = dict()
        self.charging_tiles = dict()
        self.sorting_tiles = dict()
        for sort_area in tree['sortingareas']:
            area_id, x, y, w, h = sort_area['areaid'], int(sort_area['x']), int(sort_area['y']), int(sort_area['w']), int(sort_area['h'])
            min_x, min_y = min(min_x, x), min(min_y, y)
            max_x, max_y = max(max_x, x), max(max_y, y)
            self.sorting_tiles[area_id] = {"x": x, "y" : y, "w" : w, "h" : h} 
        for charger in tree['chargers']:
            station_id, x, y = charger['id'], float(charger['locationx']), float(charger['locationy'])
            x, y = round(x), round(y) #i didn't get why the chargers have float coeffiscints
            min_x, min_y = min(min_x, x), min(min_y, y)
            max_x, max_y = max(max_x, x), max(max_y, y)
            self.charging_tiles[station_id] = {"tile" : None, "x": x, "y" : y}
        for station in tree['virtualstations']:
            station_id, x, y, direction = station['id'], int(station['locationx']), int(station['locationy']), int(station['beltDirection'])
            min_x, min_y = min(min_x, x), min(min_y, y)
            max_x, max_y = max(max_x, x), max(max_y, y)
            self.station_tiles[station_id] = {"tile" : None, "x": x, "y" : y, 'direction' : direction}  
        
        # i also decided to optimize the map by removing borders of zeros  
        while self.tile_type_map_.size > 0 and np.all((self.tile_type_map_[-1] == 0)) and self.tile_type_map_.shape[0] > max_y+1:
            self.tile_type_map_ = np.delete(self.tile_type_map_, -1, 0)
        while self.tile_type_map_.size > 0 and np.all((self.tile_type_map_[:, -1] == 0)) and self.tile_type_map_.shape[1] > max_x+1:
            self.tile_type_map_ = np.delete(self.tile_type_map_, -1, 1)          
        left_shift = 0
        up_shift = 0
        while self.tile_type_map_.size > 0 and np.all((self.tile_type_map_[0] == 0)) and up_shift<min_y-1:
            up_shift += 1
            self.tile_type_map_ = np.delete(self.tile_type_map_, 0, 0)
        while self.tile_type_map_.size > 0 and np.all((self.tile_type_map_[:, 0] == 0)) and left_shift<min_x-1:
            left_shift += 1
            self.tile_type_map_ = np.delete(self.tile_type_map_, 0, 1)
        self.left_shift_ = left_shift 
        self.up_shift_ = up_shift 
        for station_id in self.station_tiles.keys():
            self.station_tiles[station_id]['x'] -= self.left_shift_
            self.station_tiles[station_id]['y'] -= self.up_shift_
        for station_id in self.charging_tiles.keys():
            self.charging_tiles[station_id]['x'] -= self.left_shift_
            self.charging_tiles[station_id]['y'] -= self.up_shift_
        for station_id in self.sorting_tiles.keys():
            self.sorting_tiles[station_id]['x'] -= self.left_shift_
            self.sorting_tiles[station_id]['y'] -= self.up_shift_       
        self.tile_height, self.tile_width = self.tile_type_map_.shape
        # create tiles based on the type_map and other info
        self.generate_tile_map()
        
        self.sending_areas = dict() #id -> tile
        direct2num = {"n" : 0, "e" : 1, "s" : 2, "w" : 3}
        for send_area in tree["sendingareas"]:
            area_id, x, y, d = send_area['areaid'], int(send_area['x']), int(send_area['y']), direct2num[send_area["send_direction"]]
            x,y = x - self.left_shift_, y - self.up_shift_
            self.get(x,y).tile_class = TileClass.SENDING_TILE
            self.get(x,y).dest_id = area_id
            self.get(x,y).direction_send = d
            self.sending_areas[area_id] = self.get(x,y)
        for destination in tree["destinations"]:
            dest_id, x, y = destination["id"], int(destination["locationx"]), int(destination["locationy"])
            x,y = x - self.left_shift_, y - self.up_shift_
            self.get(x,y).tile_class = TileClass.DESTINATION
            self.get(x,y).dest_id = dest_id
           
    def generate_tile_map(self):
        pr_tile_id = 0
        number_tiles = self.tile_type_map_.size
        self.tile_map = [[0]*self.tile_width for _ in range(self.tile_height)]
        for x in range(self.tile_width):
            for y in range(self.tile_height):
                self.tile_map[y][x] = Tile(self.env, pr_tile_id, self.tile_type_map_[y, x], x, y)
                pr_tile_id += 1  
            
        for charger_id in self.charging_tiles.keys():
            x, y = self.charging_tiles[charger_id]['x'], self.charging_tiles[charger_id]['y']
            self.tile_map[y][x].tile_class = TileClass.CHARGE_TILE
            self.tile_map[y][x].charger_id = charger_id
            self.charging_tiles[charger_id]["tile"] = self.tile_map[y][x]
            
        for station_id in self.station_tiles.keys():
            x, y = self.station_tiles[station_id]['x'], self.station_tiles[station_id]['y']
            self.tile_map[y][x].tile_class = TileClass.STATION_TILE
            self.tile_map[y][x].station_id = station_id
            self.station_tiles[station_id]["tile"] = self.tile_map[y][x]
        
                   
            
        self.set_tile_neighbours_()
        
    def generate_queue_pathes(self):
        # list of {"receiver_id" : int, "receiver_direction" : int, "path" : [(x1, y1), (x2, y2) ... ]}  
        queue_pathes = []
        for area_id in self.sorting_tiles.keys():
            x, y, w, h = self.sorting_tiles[area_id]['x'], self.sorting_tiles[area_id]['y'], self.sorting_tiles[area_id]['w'], self.sorting_tiles[area_id]['h']
            station_id = self.tile_map[y][x].station_id
            direction = -self.station_tiles[station_id]['direction']
            queue_pathes.append({"receiver_id" : station_id, "receiver_direction" : (direction + 4)%4, "path" : self.generate_queue_path(x, y, w, h, direction)})
        return queue_pathes
    
    def set_tile_neighbours_(self):
        for x in range(self.tile_width):
            for y in range(self.tile_height):
                for direction in {0, 1, 2, 3}:
                    if direction == 0 and y > 0:
                        self.tile_map[y][x].neighbours[direction] = self.tile_map[y-1][x]
                    elif direction == 1 and x < self.tile_width - 1:
                        self.tile_map[y][x].neighbours[direction] = self.tile_map[y][x + 1]
                    elif direction == 2 and y < self.tile_height - 1:
                        self.tile_map[y][x].neighbours[direction] = self.tile_map[y+1][x]
                    elif direction == 3 and x > 0:
                        self.tile_map[y][x].neighbours[direction] = self.tile_map[y][x-1]
        
    def get(self, x, y):
        return self.tile_map[y][x]
    
    def generate_queue_path(self, conveyer_x, conveyer_y, w, h, direction = -1):
        # list of {"receiver_id" : int, "receiver_direction" : int, "path" : [(x1, y1), (x2, y2) ... ]}        
        """
        dir = -1 |  -0
                 |  |
                 |--|
        
        dir = 1 0-|  |
                  |  |
                  |--|
        """
        path = []
        start_x = conveyer_x + direction*(w-1)
        start_y = conveyer_y
        if w%2 == 1:
            start_y = conveyer_y + h - 1
            for i in range(h):
                path.append((start_x, start_y))
                start_y -= 1
            start_y += 1
            start_x -= direction
        while start_x != conveyer_x - direction:
            for i in range(h):
                path.append((start_x, start_y))
                start_y += 1
            start_y -= 1
            start_x -= direction
            for i in range(h):
                path.append((start_x, start_y))
                start_y -= 1
            start_y += 1
            start_x -= direction 
        path = list(map(lambda c: (c[0] + self.left_shift_, c[1] + self.up_shift_), path))
        return path
    
    def print(self, all_types = False):
        for x in range(self.tile_width):
            for y in range(self.tile_height):
                type_id = None
                if all_types:
                    type_id = self.tile_type_map_[y][x]
                else:
                    type_id = (self.tile_map[y][x].tile_class == TileClass.PATH_TILE)
                print("%2d"%type_id, end = "  ")
            print()
                        
    def show_classes(self):
        classes = np.zeros_like(self.tile_type_map_)
        for x in range(self.tile_width):
            for y in range(self.tile_height):        
                classes[y, x] = self.tile_map[y][x].tile_class._value_
        plt.imshow(classes, cmap='hot', interpolation='nearest')
        plt.show() 
        
    def show(self):
        classes = np.zeros_like(self.tile_type_map_)
        for x in range(self.tile_width):
            for y in range(self.tile_height):        
                if self.tile_map[y][x].robot is None:
                    classes[y, x] = self.tile_map[y][x].tile_class._value_
                else:
                    classes[y, x] = 10
                
        plt.imshow(classes, cmap='hot', interpolation='nearest')
        plt.show()     
            
    def get_tiles_by_class(self, tile_class = TileClass.PATH_TILE):
        tiles = np.array(self.tile_map).flatten()
        tiles = tiles[[(a.tile_class == tile_class) for a in tiles]]
        return tiles
    def get_tiles_by_type(self, tile_type = TileState.SHELF_CELL):
        tiles = np.array(self.tile_map).flatten()
        tiles = tiles[[(a.tile_type == tile_type) for a in tiles]]
        return tiles    
    
    @property
    def size(self):
        return self.tile_height * self.tile_width
        
    def get_map_classes(self):
        return list(map(lambda row: list(map(lambda tile: tile.tile_class, row)), self.tile_map))
    
    def get_map_coloring_files(self):
        return [[os.path.join(os.path.normpath(r"E:\E\Copy\PyCharm\RoboPost\PostSimulation\data\simulation_data\tile_type_pictures"), TILE_TYPE_PICTURES[tile.tile_type._value_]) for tile in self.tile_map[i]] for i in range(len(self.tile_map))]

from main.tools.generators import save_robot_configuration, save_queue_config
def generate_random_robot_config_on_free_tiles(map_path, save_path = "robot_v0.xml", number_robots = 5):
    map_ = PostMap(simpy.Environment(), map_file_path = map_path)
    tile_pos = np.random.choice(map_.get_tiles_by_class(TileClass.PATH_TILE), number_robots)
    robot_tiles = [(t.x, t.y) for t in tile_pos]
    data = []
    for i in range(number_robots):
        data.append({'robot_id': "rob_"+str(i), 'x': robot_tiles[i][0] + map_.left_shift_, 'y': robot_tiles[i][1] + map_.up_shift_, 'direction' : random.randint(0, 3)})
    save_robot_configuration(save_path, data)
    
if __name__ == "__main__":
    map_file_path=r"E:\E\Copy\PyCharm\RoboPost\PostSimulation\data\simulation_data\sim_v0\map_v0.xml"
    map_ = PostMap(simpy.Environment(), map_file_path = map_file_path)
    """
    pot_tiles = map_.get_tiles_by_type()
    left_tiles = list(filter(lambda tile: tile.x == 0 or tile.x == 18 or tile.x == 36, pot_tiles))
    up_tiles = list(filter(lambda tile: tile.y == 0, pot_tiles))
    right_tiles = list(filter(lambda tile: tile.x == 12 or tile.x == 30 or tile.x == map_.tile_width-1, pot_tiles))
    number_tile = 0
    for tile in left_tiles:
        print("    <destination>")
        print("      " + f'<id>"{"D" + str(number_tile)}"</id>')
        print("      " + f'<locationx>{tile.x + map_.left_shift_}</locationx>')
        print("      " + f'<locationy>{tile.y + map_.up_shift_}</locationy>')
        print("      " + f'<send_direction>"w"</send_direction>')
        print("    </destination>")
        print(f'      <sendingarea areaid="{"D" + str(number_tile)}" x="{tile.x + map_.left_shift_ + 1}"  y="{tile.y + map_.up_shift_}" z="1" send_direction="w" />')
        number_tile += 1
    """
    #generate_random_robot_config_on_free_tiles(map_file_path, r'E:\E\Copy\PyCharm\RoboPost\PostSimulation\data\simulation_data\sim_v0\robots_2.xml', 5)
    #data = map_.generate_queue_pathes()
    fpath = r'E:\E\Copy\PyCharm\RoboPost\PostSimulation\data\simulation_data\sim_v0\queue_v0.xml'
    #save_queue_config(fpath, data)
    