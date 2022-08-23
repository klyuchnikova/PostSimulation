import simpy 
import datetime
import random
import shelve
from enum import Enum
from main.tools.loaders import load_dws_configuration

class RobotCommand(Enum):
    MOVE = 1
    STAND_IN_QUEUE = 2
    RECEIVE_PACKAGE = 3
    SEND_PACKAGE = 4
    CHARGE = 5

class RobotState(Enum):
    FREE = 1
    TAKING_PACKAGE = 2
    TRANSPORTING = 3
    SENDING_PACKAGE = 4
    CHARGING = 5
    QUEUING = 6

class Robot:
    TAKING_PACKAGE_TIMEOUT = 2
    MOVING_ONE_TILE_TIMEOUT = 1
    TURNING_ONE_TIMEOUT = 1
    SENDING_PACKAGE_TIMEOUT = 2
    
    """robot right now only has the knowledge of next move received from mysterious system"""
    def __init__(self, env, robot_id, robot_o_id, start_position_tile, start_direction = 1, speed = 1, charge = 100):
        assert 0<=start_direction<=3  # 1 is North, 2 is East, 3 is South, 4 is West
        
        self.env = env
        self.robot_id = robot_id
        self.robot_o_id = robot_o_id
        self.position = start_position_tile
        self.position.init_move_in(self)
        self.direction = start_direction
        self.state = RobotState.FREE
        self.speed = speed
        self.charge = charge
        self.containing_package_id = None
        self.blocked = False
        self.printing_events = False      
        
    def move_forward(self, **kwargs):
        if self.printing_events:
            print(f"{self.env.now}: robot {self.robot_id} moving forward from {self.position.x}, {self.position.y}, {self.direction}")
        next_tile = self.position.neighbours[self.direction]
        if next_tile is None:
            yield Exception(f"{self.robot_id} from position {self.position.tile_id} no tile in the facing direction {self.direction}")
        else:
            yield self.env.process(next_tile.request_move_in(self))
            yield self.env.timeout(Robot.MOVING_ONE_TILE_TIMEOUT)
            yield self.env.process(self.position.request_move_out(self))
            self.position = next_tile  
        if self.printing_events:
            print(f"{self.env.now}: robot {self.robot_id} moved in {self.position.x}, {self.position.y}, {self.direction}")
        return True
                
    def turn(self, right = True):
        if self.printing_events:
            print(f"{self.env.now}: robot {self.robot_id} turning {self.direction} right = {right}")
        # direction is either left or right
        yield self.env.timeout(Robot.TURNING_ONE_TIMEOUT)
        if right:
            self.direction = (self.direction + 1)%4
        else:
            self.direction = (self.direction - 1)%4
        if self.printing_events:
            print(f"{self.env.now}: robot {self.robot_id} turned {self.direction} right = {right}")
    
    def receive_package(self, package_id):
        if self.printing_events:
            print(f"{self.env.now}: receive package start ({Robot.TAKING_PACKAGE_TIMEOUT})")
        yield self.env.timeout(Robot.TAKING_PACKAGE_TIMEOUT)
        self.containing_package_id = package_id
        if self.printing_events:
            print(f"{self.env.now}: bot {self.robot_id} recieved {package_id}")
        
    def send_package(self, package_id):
        if self.printing_events:
            print(f"{self.env.now}: send package start ({Robot.SENDING_PACKAGE_TIMEOUT})")
        yield self.env.timeout(Robot.SENDING_PACKAGE_TIMEOUT)
        self.containing_package_id = None
        if self.printing_events:
            print(f"{self.env.now}: bot {self.robot_id} sent {package_id}")
  
class DWS_communicator:
    SYSTEM_TYPES = ['SERVER', 'FROM_FILE', 'DEFINE']
    
    def __init__(self, input_type = "FROM_FILE", fpath = None, server = None, **kwargs):
        assert input_type in DWS_communicator.SYSTEM_TYPES
        self.input_type = input_type
        
        self.events_ = dict()
        if self.input_type == "FROM_FILE":
            self.events_ = load_dws_configuration(fpath)
        else:
            pass
        
    def get_start_date(self):
        return min(self.events_.keys())
    def get_end_date(self):
        return max(self.events_.keys())    
    def split_events_by_ticks(self, start_date, end_date, tick_duration):
        number_ticks = int(((end_date - start_date).total_seconds() + tick_duration - 1)//tick_duration)
        self.tick_events_ = [[] for _ in range(number_ticks)]
        for date in self.events_.keys():
            tick_id = int((date - start_date).total_seconds()//tick_duration)
            if 0 <= tick_id < number_ticks:
                self.tick_events_[tick_id].extend(self.events_[date])
        
    def recieve_moment_events_by_date(self, datetime_moment):
        # returns a list of the events
        return self.events_.get(datetime_moment, [])
    
    def receive_tick_events(self, tick_id):
        return self.tick_events_[tick_id]
    
