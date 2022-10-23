import numpy as np
import json
from json import JSONEncoder
import os
from main.entities.post_map import TileClass

""" Idea is that the map is devided into intersecting cycles with vertical lines used to go straight to queues and out of station tiles 
    (also possibly horizontal for charges). The sorting tiles are devided into 9 zones, for each there's a fixed logic of going to 
    th queue (for pair zone-station there's a path consisting of 'path' object names. Transportation between path objects can be realised 
    either by presetup rules written in text files or with build in functions)"""

def in_between(x, y, z):
    return (x <= y <= z or x >= y >= z)

def intersect_horizontal_with_vertical_sides(y, x1, x2, x, y1, y2):
    if (in_between(x1, x, x2) and in_between(y1, y, y2)):
        return (x, y)
    else:
        return None
    
def get_intersections(start_1, end_1, start_2, end_2):
    # get intersection between two lines (if none returns [], if lines are aligned then returns two points: closest to start_1 and furthes)
    if (start_2[0] > end_2[0] or start_2[1] > end_2[1]):
        start_2, end_2  = end_2, start_2
    reverse_orientation = False
    if (start_1[0] > end_1[0] or start_1[1] > end_1[1]):
        start_1, end_1  = end_1, start_1    
        reverse_orientation = True
        
    if end_2 == start_1:
        return [start_1,] 
    if start_2 == end_2:
        return [start_2,]    
        
    if (start_1[0] == end_1[0]):
        # first vertical
        if (start_2[1] == end[1]):
            # second horizontal
            if in_between(start_1[1], start_2[1], end_1[1]) and in_between(start_2[0], start_1[0], end_2[0]):
                return [(start_1[0], start_1[1])]
            else:
                return []
        else:
            intersections = []
            if end_2[1] > start_1[1] or end_1[1] > start_2[1]:
                return []
        
            if in_between(start_1[1], start_2[1], end_1[1]):
                intersections.append(start_2)
            else:
                intersections.append(start_1)
            if in_between(start_1[1], end_2[1], end_1[1]):
                intersections.append(end_2)
            else:
                intersections.append(end_1)
                
            if reverse_orientation:
                intersections = intersections[::-1]        
            return intersections
    else:
        # first line is horizontal
        if (start_2[1] == end[1]):
            # second horizontal
            intersections = []
            if end_2[0] > start_1[0] or end_1[0] > start_2[0]:
                return []
        
            if in_between(start_1[0], start_2[0], end_1[0]):
                intersections.append(start_2)
            else:
                intersections.append(start_1)
            if in_between(start_1[0], end_2[0], end_1[0]):
                intersections.append(end_2)
            else:
                intersections.append(end_1)  
                
            if reverse_orientation:
                intersections = intersections[::-1]            
            return intersections
        else:
            if in_between(start_1[0], start_2[0], end_1[0]) and in_between(start_2[1], start_1[1], end_2[1]):
                return [(start_2[0], start_1[1])]
            else:
                return []            
           
        
    
def get_intersections(point_1, point_2, cycle):
    # point_1 and point_2 are ends of a horizontal or vertical edge, we find all intersections with cycle and return a list
    # !vertices in list are in order of clockwise movement on the edge!
    intersections = []
    if point_1[0] == point_2[0]:
        intersect = intersect_horizontal_with_vertical_sides(cycle.y1, cycle.x1, cycle.x2, point_1[0], point_1[1], point_2[1]) # first vertical
        if intersect is not None:
            intersections.append(intersect)
        intersect = intersect_horizontal_with_vertical_sides(cycle.y2, cycle.x1, cycle.x2, point_1[0], point_1[1], point_2[1]) # second vertical
        if intersect is not None:
            intersections.append(intersect)
    else:
        intersect = intersect_horizontal_with_vertical_sides(point_1[1],  point_1[0],  point_2[0], cycle.x1, cycle.y1, cycle.y2) # first horizontal
        if intersect is not None:
            intersections.append(intersect)
        intersect = intersect_horizontal_with_vertical_sides(point_2[1],  point_1[0],  point_2[0], cycle.x1, cycle.y1, cycle.y2) #second horizontal
        if intersect is not None:
            intersections.append(intersect)
    return intersections
   

class PathObject:
    def __init__(self, name, intersections = None):
        self.name = name
        if intersections is None:
            self.intersections = dict() # name to a tuple of points
        else:
            self.intersections = intersections
        
    def build_intersections(self, path_object):
        intersections = []
        for line in self.get_lines():
            for line_2 in path_object.get_lines():
                intersections.extend(get_intersections(*line, *line_2))
        self.intersections[path_object.name] = intersections
        
    def get_lines(self):
        # returns tuple of lines - each is a tuple of two points (2d tuple)
        raise NotImplementedError
    
    def parameters(self):
        # returns dict of parameters given in constructor
        raise NotImplementedError        
            

class Cycle(PathObject):
    def __init__(self, name, x1, y1, x2, y2, direction):
        # left top is (x1, y1) and right top is (x2, y2):
        super().__init__(name)
        self.x1 = x1
        self.y1 = y1
        self.x2 = x2
        self.y2 = y2
        #self.right_bottom = (y1, x1) # coordinates in matrix are transposed
        #self.left_top = (y2, x2)
        self.direction = direction # 1 for clockwise or 0 for not
        # for example (0,0) (4,4) direction=1 means 0,0 -> 4,0 -> 4,4 -> 0,4
    
    def parameters(self):
        return {'name': self.name, 'x1': self.x1, 'y1' : self.y1, 'x2':self.x2, 'y2':self.y2, 'direction':self.direction}
    
    def get_lines(self):
        lines = (((x_1, y_1), (x_2, y_1)), 
                 ((x_2, y_1), (x_2, y_2)), 
                 ((x_2, y_2), (x_1, y_2)),
                 ((x_1, y_2), (x_1, y_1)))
        if self.direction:
            return lines
        else:
            return lines[::-1]
    
    def build_path_to_x(self, start_point, x, path):
        #considering start_point is on this cycle try to move to a point with specific x
        if start_point[0] == x:
            return
        # returns path in originally discussed format
        if start_point.y != self.y1 and start_point.y != self.y2:
            # go to the horizontal line first
            if start_point.x == self.x1:
                if self.direction:
                    # move down
                    start_point = (self.x1, self.y1)
                else:
                    start_point = (self.x1, self.y2)
            else:
                if self.direction:
                    start_point = (self.x2, self.y2)
                else:
                    start_point = (self.x2, self.y1)
            path.append((0, start_point))  
        if ((start_point[0] > x) ^ direction):
            # we can move straight without rotating around curve
            path.append((0, (x, start_point[1]))) 
        else:
            # first move to another y level
            if start_point.x == self.x1:
                if self.direction:
                    # move down
                    start_point = (self.x1, self.y1)
                else:
                    start_point = (self.x1, self.y2)
            else:
                if self.direction:
                    start_point = (self.x2, self.y2)
                else:
                    start_point = (self.x2, self.y1) 
            path.append((0, start_point))
            self.build_path_to_x(start_point, x, path)
            
    def build_path_to_y(self, start_point, y, path):
        if start_point[1] == y:
            return
        # returns path in originally discussed format
        if start_point.x != self.x1 and start_point.x != self.x2:
            # go to the vertical line first
            if start_point.y == self.y1:
                if self.direction:
                    # move left
                    start_point = (self.x2, self.y1)
                else:
                    start_point = (self.x1, self.y1)
            else:
                if self.direction:
                    start_point = (self.x1, self.y2)
                else:
                    start_point = (self.x2, self.y2)
            path.append((0, start_point))  
        if ((start_point[1] < y) ^ direction):
            # we can move straight without rotating around curve
            path.append((0, (start_point[0], y))) 
        else:
            # first move to another y level
            if start_point.y == self.y1:
                if self.direction:
                    # move left
                    start_point = (self.x2, self.y1)
                else:
                    start_point = (self.x1, self.y1)
            else:
                if self.direction:
                    start_point = (self.x1, self.y2)
                else:
                    start_point = (self.x2, self.y2)
            path.append((0, start_point))  
            self.build_path_to_y(start_point, y, path)
            
        def get_cycle_path(self, start_point):
            # build cycle path from stop points
            path = None
            if (start_point == (self.x1, self.y1)):
                path = [(self.x2, self.y1), (self.x2, self.y2), (self.x1, self.y2)]
            elif (start_point == (self.x2, self.y1)):
                path = [(self.x2, self.y2), (self.x1, self.y2), (self.x1, self.y1)]
            elif (start_point == (self.x2, self.y2)): 
                path = [(self.x1, self.y2), (self.x1, self.y1), (self.x2, self.y1)]
            elif (start_point == (self.x1, self.y2)): 
                path = [(self.x1, self.y1), (self.x2, self.y1), (self.x2, self.y2)]
            elif start_point[0] == self.x1:
                path = [(self.x2, self.y1), (self.x2, self.y2), (self.x1, self.y2), (self.x1, self.y1)]
            elif start_point[0] == self.x2:
                path = [(self.x1, self.y2), (self.x1, self.y1), (self.x2, self.y1), (self.x2, self.y2)]
            elif start_point[1] == self.y1:
                path = [(self.x1, self.y1), (self.x2, self.y1), (self.x2, self.y2), (self.x1, self.y2)]
            else:
                path = [(self.x2, self.y2), (self.x1, self.y2), (self.x1, self.y1), (self.x2, self.y1)]
            if (self.direction != 1):
                path = path[::-1]
            path.push_back(start_point)
            return path
            
        
        def build_shortest_path_to(self, start_point, other_cycle):
            """supposing we want to go from this cycle to another one, we're building shortest path IN TYLES"""
            # horizontal with vertical
            intersect_point = None
            point_1 = start_point
            path = []
            for point_2 in get_cycle_path(start_point):
                intersect_points = get_intersections(point_1, point_2, other_cycle)
                if len(intersect_points != 0):
                    if self.direction:
                        intersect_point = intersect_point[0]
                    else:
                        intersect_point = intersect_point[-1]
                    break
                path.append(point_2)
                point_1 = point_2
            if intersect_point is None:
                return None
            path.append(intersect_point)
            return path
            
        def build_path_to(self, start_point, other_cycle):
            """supposing we want to go from this cycle to another one, we're building shortest path considering rotations 
            (we may go through the first intersection in order to minimize number of rotations)"""
            pass
        
class VerticalLine(PathObject):
    def __init__(self, name, x, y1, y2, direction):
        # y_1 < y_2, direction = 1 means we go down, 0 - up
        super().__init__(name)
        self.x = x
        self.y1 = y1
        self.y2 = y2
        self.direction = direction
        
    def parameters(self):
        return {'name': self.name, 'x': self.x, 'y1' : self.y1, 'y2':self.y2, 'direction':self.direction}    
        
    def get_lines(self):
        if self.direction:
            return ( ((x, y_1), (x, y_2)),)
        else:
            return ( ((x, y_2), (x, y_1)),)
   
def InitPathObject(args):
    if set(args.keys()) == {'name', 'x', 'y1', 'y2', 'direction'}:
        return VerticalLine(*args)
    else:
        return Cycle(*args)

def reverse_dict(dictionary):
    # dict (key: value) -> dict (value : list of keys) 
    new_keys = set(dictionary.values()) 
    new_dict = dict.fromkeys(new_keys, list())
    for v, k in dictionary.items():
        new_dict[k].append(v)
    return new_dict

def unreverse_dict(dictionary):
    new_dict = dict()
    for v, keys in dictionary.items():
        for key in keys:
            try:
                new_dict[tuple(key)] = int(v)
            except:
                new_dict[tuple(key)] = v
    return new_dict

class CycleMap:
    def __init__(self, map_controller, save_dir = None):
        self.map_controller = map_controller
        self.map_height = 35
        self.map_width = 49
        self.groups = {(0, 0): 2, (5,5):3} # point : group id
        path_objects = [{"name": 5, "x1": 0, "y1":0, "x2": 5, "y2":6, "direction": 1}] # list of args -> dict of objects 
        self.path_objects = dict()
        for param_set in path_objects:
            self.path_objects[param_set['name']] = InitPathObject(**param_set)       
        self.group2group = {(2, 3) : [5] }
        self.object2object = dict() # optional
        
        if save_dir is None:
            # load default
            self.file_path = os.path.join(os.path.dirname(os.path.abspath(__file__)), os.path.normpath("..\\..\\data\\simulation_data\\default\\cyclemap.json"))
        else:
            self.file_path = os.path.join(os.path.normpath(save_dir), "cyclemap.json")
        #self.number_nodes = self.map_controller.size
        
        if os.path.exists(os.path.normpath(self.file_path)):
            self.load_from_file(self.file_path)
            if len(self.object2object) == 0:
                self.init_object2object()
        else:
            try:
                self.save_state()
            except:
                os.remove(self.file_path)
            raise FileNotFoundError
       
    def load_from_file(self, file_path):
        with open(file_path, "r") as file:
            f = json.load(file)
            for key, val in f.items():
                self.__dict__[key] = val
            path_objects = dict()
            for param_set in self.path_objects:
                path_objects[param_set["name"]] = InitPathObject(**param_set)
            self.groups = unreverse_dict(self.groups)
            self.group2group = { tuple(point): tuple(name_order) for point, name_order in self.group2group}
            self.path_objects = path_objects
        
    def save_state(self):
        with open(self.file_path, "w") as f:
            json.dump({"map_height" : self.map_height, 
                       "map_width" : self.map_width, 
                       "groups" : reverse_dict(self.groups), 
                       "path_objects" : list(map(lambda obj: obj.parameters(), self.path_objects.values())),
                       "group2group" : list(self.group2group.items()),
                       "object2object" : self.object2object}
                        , f, indent = 4)
    
    def init_object2object(self):
        for obj in self.path_objects.values():
            for other in self.path_objects.values():
                if (obj != other):
                    obj.build_intersections(other)
    
    def tile2id(self, tile):
        return tile.y*self.map_width + tile.x
    def id2coords(self, tile_id):
        tile_id = int(tile_id)
        return tile_id%self.map_width, tile_id//self.map_width
    def coords2id(self, coords):
        return coords[1]*self.map_width + coords[0]
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
    
if __name__ == '__main__':
    map_ = CycleMap(5)
