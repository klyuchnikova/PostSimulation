import simpy 
import datetime
import random
from collections import deque
from main.entities.actors import *
from main.entities.post_map import *
from main.tools.loaders import load_robot_configuration, load_queue_areas
from main.tools.generators import save_robot_configuration

class QueueAreaController:
    def __init__(self, env, robot_controller, receiver_id, receiver_d, map_, queue_path):
        """
        The trick is to create a tile with capacity of queue_path and replace the corresponding neighbour 
        of each of border tiles to this same one. Instead of moving into tiles of queue robots will be moving into this particular tile and 
        at this point there will be an option of taking a first robot of queue out or getting it by id (although it only has meaning in some
        over thinked calculations with charges... i simply find it redundant)
        """
        # queue_path is [(x,y), ... ()] where final is station reciever area
        # init_state is possible robot queue already standing
        self.env = env
        self.robot_controller = robot_controller
        self.receiver_id = receiver_id
        self.receiver_in_direction = (2-receiver_d)%4
        self.robot_order_ = deque()
        self.map_ = map_
        self.path = []
        self.robot_arrival_event = dict()
        
        # first arrange path - build it from tiles
        self.arrange_queue_path(queue_path)
        # then when self.path is set 1) move all the robots standing in the path tiles out and add them to robot_order_
        self.clear_queue_area()
        # create queue tile
        self.queue_tile = None
        self.create_queue_tile()
        # change neighbour connections of bounderies
        self.arrange_neighbour_connections_()
        
    def arrange_queue_path(self, queue_path):
        self.queue_length = len(queue_path)
        self.path = [0]*len(queue_path)
        for i, tile_xy in enumerate(queue_path):
            x,y = tile_xy
            x,y = int(x) - self.map_.left_shift_, int(y) - self.map_.up_shift_          
            self.path[i] = self.map_.get(x,y)
            self.path[i].tile_class = TileClass.QUEUE_TILE
        self.receiver_x, self.receiver_y = self.path[-1].x, self.path[-1].y
            
    def create_queue_tile(self):
        self.queue_tile = QueueTile(env = self.env, queue_controller = self, tile_id = 10**6 + self.receiver_id, 
                                    reciever_tile = self.path[-1], 
                                    queue_size = self.queue_length)
            
    def arrange_neighbour_connections_(self):
        for tile in self.path:
            for direction in range(4):
                if tile.neighbours[direction]:
                    tile.neighbours[direction].neighbours[(direction + 2)%4] = self.queue_tile
            
    def clear_queue_area(self):
        for tile in self.path:
            if tile.robot is not None:
                self.robot_order_.append(tile.robot)
                yield self.queue_tile.request_move_in(tile.robot)
                tile.robot.position.robot = None
                tile.robot.position = self.queue_tile
        
    def move_in_robot_(self, robot, tile):
        # robot must already be on the tile inside Queue area
        self.robot_order_.append(robot)
        # because of difficulties with occupation matrix we have to at some point reallocate robot at the beginning of the queue
        self.robot_controller.assighn_robot_position(robot, self.receiver_x, self.receiver_y, self.receiver_in_direction)
        self.robot_arrival_event.setdefault(robot.robot_id, self.env.event()).succeed()
    def remove_first_robot_(self):
        self.robot_order_.pop()
    def remove_last_robot_(self):
        self.robot_order_.popleft()
    def remove_robot_(self, robot):
        # i suppose that will never be used
        self.robot_order_.remove(self.robot_order_.index(robot))    
        
    def get_first_in_queue(self):
        if len(self.robot_order_) > 0:
            return self.robot_order_[-1]
        else:
            return None
    def get_last_in_queue(self):
        if len(self.robot_order_) > 0:
            return self.robot_order_[0]
        else:
            return None
    @property
    def queue_size(self):
        return len(self.robot_order_)
    
    def process_path_request(self, commands, robot_id = None):
        # the part where queue takes charge of queueing requests to deliver packages
        # 1. wait till the robot is ready at the reciever
        # 2. call controller to add the commands with robot_id to pathes (during the tick or not)
        print("process_path_request: ", commands, robot_id )
        if robot_id is None:
            # simply wait till some robot is in queue
            self.env.process(self.await_first_in_queue_to_command(commands))
        else:
            # await for particular robot to arrive
            self.env.process(self.await_robot_in_queue_to_command(robot_id, commands))
    
    def await_first_in_queue_to_command(self, commands):
        request = self.queue_tile.container.get(1)
        yield request
        self.robot_controller.assighn_path_to(self.get_first_in_queue.robot_id, commands)
        yield self.queue_tile.container.release(request)
        
    def await_robot_in_queue_to_command(self, robot_id, commands):
        yield self.robot_arrival_event[robot_id]
        self.robot_controller.assighn_path_to(robot_id, commands)

class RobotController:
    def crete_robots_(self, start_config):
        # input must be list of dicts like {"robot_id": "", "x" : int, "y" : int, "direction" : int}
        self.number_robots = len(start_config)
        self.robot_id2order = dict()
        self.robot_order2id = np.array([""]*self.number_robots)
        self.robot_positions_ = [0]*self.number_robots
        self.robot_directions_ = np.array([0]*self.number_robots)
        self.robots_ = [0]*self.number_robots
        directotions_id = {'n' : 0, 'w' : 1, 's' : 2, 'e' : 3}
        for i, robot_conf in enumerate(start_config):
            assert self.robot_id2order.get(robot_conf["robot_id"], None) is None
            robot_id, x, y, direction = robot_conf["robot_id"], int(robot_conf["x"]), int(robot_conf["y"]), int(robot_conf["direction"])
            self.robot_id2order[robot_id] = i
            self.robot_order2id[i] = robot_id
            self.robot_positions_[i] = (x - self.map_.left_shift_, y - self.map_.up_shift_)
            self.robot_directions_[i] = direction
            self.robots_[i] = Robot(self.env, robot_conf["robot_id"], i, start_position_tile = self.map_.get(self.robot_positions_[i][0], self.robot_positions_[i][1]), start_direction = direction)
        self.robot_positions_ = np.array(self.robot_positions_)
        self.robots_ = np.array(self.robots_)
        self.robot_responses = np.array([True]*len(self.robots_))
        self.pathes_ = [deque() for _ in range(self.number_robots)]
        self.current_commands_ = [None for _ in range(self.number_robots)]
        
    def create_queue_controllers(self, start_config):
        # list of {"receiver_id" : int, "receiver_direction" : int, "path" : [(x1, y1), (x2, y2) ... ]}
        self.number_queues = len(start_config)
        self.queue_controllers = dict()
        directotions_id = {'n' : 0, 'w' : 1, 's' : 2, 'e' : 3}
        for i, queue_conf in enumerate(start_config):
            conv_id, receiver_d, queue_path = int(queue_conf["receiver_id"]), int(queue_conf["receiver_direction"]) , queue_conf["path"]
            self.queue_controllers[conv_id] = QueueAreaController(self, self.env, conv_id, receiver_d, self.map_, queue_path)
            
    
    def __init__(self, env, map_, queues_init_file, robot_init_file, config_vars):
        self.env = env
        self.map_ = map_
        self.init_file_path = robot_init_file
        self.queues_init_file = queues_init_file
        
        for robot_var_name, val in config_vars.get('robot_controller', {}).items():
            RobotController.__dict__[robot_var_name] = val        
        for robot_var_name, val in config_vars.get('robot', {}).items():
            setattr(Robot, robot_var_name, val)
            
        """There are two ids for each robot - first is robot_id loaded from init file. 
        It is used to 1) represent robots on map 2) send to WMS
        Secondly we have robot_o_id which is simply a number from 0 to number of robots. 
        The number can be found in robot_id2order and id in robot_order2id"""
        self.robot_id2order = dict()
        self.robot_order2id = []
           
        self.number_robots = 0 
        self.robot_positions_ = np.array([]) # robot_o_id -> (x,y) # mostly for logs
        self.robots_ = np.array([]) # robot_o_id -> Robot
        self.pathes_ = [] # robot_o_id -> path where path is a dequeue
        self.current_commands_ = np.array([]) # robot_o_id -> command on this tick (filtered from pathes with received answers)
        self.robot_responses = np.array([]) # robot_o_id -> bool # has the response come?
        
        self.crete_robots_(load_robot_configuration(self.init_file_path))
        
        self.queue_controllers = dict() # receiver_id -> controller
        self.create_queue_controllers(load_queue_areas(self.queues_init_file))
        
        self.occupation_map_ = np.zeros((self.map_.tile_height, self.map_.tile_width))
        # 0 - free, 1 - occupied with robot on current tick, 2 - occupied by command on next move
        self.init_occupation_map()
        
    def init_occupation_map(self):
        for x in range(self.map_.tile_width):
            for y in range(self.map_.tile_height):
                if self.map_.get(x,y).robot and self.map_.get(x,y).tile_class != TileClass.QUEUE_TILE:
                    self.occupation_map_[y, x] += 1
    def update_occupation_map_and_positions(self):
        for robot_o_id in range(self.number_robots):
            if self.robot_responses[robot_o_id] and self.current_commands_[robot_o_id]:
                if self.current_commands_[robot_o_id][0] == "move_forward":
                    d = self.robot_directions_[robot_o_id]
                    if self.map_.get(*self.robot_positions_[robot_o_id]).tile_class != TileClass.QUEUE_TILE:
                        self.occupation_map_[self.robot_positions_[robot_o_id][1], self.robot_positions_[robot_o_id][0]] -=1
                    self.robot_positions_[robot_o_id] = (self.robot_positions_[robot_o_id][0] - (d%2)*(d - 2), self.robot_positions_[robot_o_id][1] + ((d+1)%2)*(d-1))
                    #self.occupation_map_[self.robot_positions_[robot_o_id][1], self.robot_positions_[robot_o_id][0]] +=1
                elif self.current_commands_[robot_o_id][0] == "turn":
                    self.robot_directions_[robot_o_id] = (self.robot_directions_[robot_o_id] + self.current_commands_[robot_o_id][1])%4
    def update_occupation_map(self):
        pass
    
    def get_robot_coordinates_by_id(self, robot_id):
        robot = self.robots_[self.robot_id2order[robot_id]]
        return (robot.position.x, robot.position.y)
    
    def assighn_robot_position(self, robot, x, y, d):
        self.robot_positions_[robot.robot_o_id] = (x,y)
        self.robot_directions_[robot.robot_o_id] = d
        if self.map_.get(x,y).tile_class != TileClass.QUEUE_TILE:
            self.occupation_map_[y, x] += 1
        
    def assighn_path_to(self, robot_id, path):
        # and HERE we parse commands cause unfortunately we'll face a problem of possibly extending the path by rotaions which we'll have to do straight away
        # this method is eventually called if robot receives any new commands
        robot_o_id = self.robot_id2order[robot_id] 
        x,y,d = self.robots_[robot_o_id].position.x, self.robots_[robot_o_id].position.y, self.robots_[robot_o_id].direction
        self.pathes_[robot_o_id].extend(self.parse_wms_robot_commands(x,y,d, path))
    
    def process_wms_command(self, command):
        robot_id, receiver_id, command  = command['robot_id'], command['receiver_id'], command['command']
        if robot_id == None:
            # we suppose that the task is to drive a package - this task is to be performed by the queue controller
            if receiver_id:
                self.self.queue_controllers[receiver_id].process_path_request(command)
            else:
                # command to particularly controller or logger?
                pass
        elif receiver_id:
            self.queue_controllers[receiver_id].process_path_request(command, robot_id)
        else:
            self.assighn_path_to(robot_id, command)
    
    def update_state_by_command(self, r_x, r_y, d, command):
        if command[0] == "move_forward":
            n_x, n_y = (r_x - (d%2)*(d - 2), r_y + ((d+1)%2)*(d-1))
            assert self.occupation_map_[n_y, n_x] == 0 and self.map_.get(n_x, n_y).tile_class != TileClass.BLOCK
            if self.map_.get(n_x, n_y).tile_class != TileClass.QUEUE_TILE:
                self.occupation_map_[n_y, n_x] += 1
        return True
            
    def send_robot_command_(self, robot_o_id, command_func_name, **kwargs):
        yield self.env.process(getattr(self.robots_[robot_o_id], command_func_name)(kwargs))
        self.robot_responses[robot_o_id] = True
        
    def send_current_commands(self):
        for robot_o_id in range(self.number_robots):
            if self.current_commands_[robot_o_id]:
                # occupy the direction in which the robot moves if it does (also checks if the move is valid)
                self.update_state_by_command(self.robots_[robot_o_id].position.x, self.robots_[robot_o_id].position.y, self.robots_[robot_o_id].direction, self.current_commands_[robot_o_id])
                # if the move is ok then make it
                print(f"{self.env.now}: sending command {self.current_commands_[robot_o_id][0]} to robot {robot_o_id}")
                self.env.process(self.send_robot_command_(robot_o_id, self.current_commands_[robot_o_id][0], **self.current_commands_[robot_o_id][1]))
                
    def make_routine_loop(self):
        print("current pathes: ", self.pathes_)
        self.update_occupation_map_and_positions() # based on PREVIOUS commands which are stored as current_commands, robot_responses
        self.update_current_commands() # we consider that dws and wms commands through tick HAVE ALREADY BEEN PROCESSED
        self.empty_robot_responses()
        print(f"current commands: ", self.current_commands_)
        self.send_current_commands()

    def update_current_commands(self):
        for robot_o_id in range(self.number_robots):
            if self.robot_responses[robot_o_id] and len(self.pathes_[robot_o_id]) > 0:
                self.current_commands_[robot_o_id] = self.pathes_[robot_o_id].popleft()
            else:
                self.current_commands_[robot_o_id] = None
    def empty_robot_responses(self):
        self.robot_responses.fill(False)
        
    @classmethod
    def optimal_rotation(cls, start_d, end_d):
        if abs((end_d-start_d)%4) <= 2:
            #turn left
            return [False]*(abs((end_d-start_d)%4))
        else:
            #turn right
            return [True]*(abs((start_d-end_d)%4))
    
    @classmethod
    def parse_wms_robot_commands(cls, robot_x, robot_y, robot_d, wms_commands):
        # current x,y,direction in coordinates of map_
        # commad like (c_type, data) 
        # c_type : 0 - move | 1 - receive pckg      | 2 - deliver pckg | 3 - charge
        # data :  (x,y)     | (receiver_id, pkg_id) |      -//-        | (chrger_id, charge_time) 
        # out there will be list of tuples (command_name, **kwargs) - (string, dict)
        prev_x = robot_x
        prev_y = robot_y
        prev_d = robot_d
        parsed_commands = []
        for command in wms_commands:
            if command[0] == 0:
                # move command
                new_x, new_y = command[1]
                if new_x < prev_x:
                    new_d = 3
                elif new_x > prev_x:
                    new_d = 1
                elif new_y > prev_y:
                    new_d = 0
                else:
                    new_d = 2
                parsed_commands.extend([("turn", {"right" : is_right}) for is_right in self.optimal_rotation(prev_d, new_d)])
                parsed_commands.append(("move_forward", dict()))      
                prev_x, prev_y, prev_d = new_x, new_y, new_d
            elif command[0] == 1:
                # receive
                parsed_commands.append(("receive_package", {"package_id" : command[1]})) 
            elif command[0] == 2:
                # deliver
                parsed_commands.append(("send_package", {"package_id" : command[1]}))
            else:
                # ???
                pass
        return parsed_commands
    
    def generate_random_configuration(self, fpath, number_robots = 5):
        robot_tiles = np.random.choice(self.map_.get_tiles_by_class(TileClass.PATH_TILE), number_robots)
        data = []
        for i in range(number_robots):
            data.append({'robot_id': "rob_"+str(i), 'x': robot_tiles[i].x + self.map_.left_shift_, 'y': robot_tiles[i].y + self.map_.up_shift_, 'direction' : random.randint(0, 3)})
        save_robot_configuration(fpath, data)        
    
        
    