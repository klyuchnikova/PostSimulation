import simpy 
import datetime
import random
import os
from main.entities.post_map import PostMap
from main.entities.logger import Logger
from main.entities.actors import DWS_communicator
from main.controllers.wms_controller import WMS_communicator
from main.controllers.robot_controller import RobotController
from main.tools.loaders import load_env_configuration
from tqdm import tqdm

class EnvController:
    run_modes = ["FIXED_END_TIME", "FIXED_NUM_TICKS", "UNTIL_WORK_COMPLETE", "UNTIL_ACTIONS_COMPLETE"]
    def __init__(self, sim_config_file_path):
        sim_config_file_path = os.path.abspath(sim_config_file_path)
        
        self.NAME = "sim_v01"
        self.START_TIME = None
        self.END_TIME = None
        self.ONE_TICK = 1.
        self.WMS_CONTROLLER_TYPE = "DEFINE" # can be "SERVER" but than there must also be "WMS_CONTROLLER_PORT"
        self.WMS_CONTROLLER_PORT = None
        self.WMS_CONFIG_PATH = None
        self.DWS_CONTROLLER_TYPE = "FROM_FILE"
        self.DWS_CONTROLLER_PORT= None
        self.DWS_CONFIG_PATH = None
        
        self.MAP_CONFIG_PATH = None
        self.DESTINATIONS_CONFIG_PATH = None
        self.ROBOT_CONFIG_PATH = None
        self.QUEUE_CONFIG_PATH = None
        #self.LOGGER_OUT_PATH = "..\\..\\data\\simulation_data\\sim_example"
        self.run_mode = self.run_modes[0]
        
        self.env_vars = load_env_configuration(sim_config_file_path)
        for robot_var_name, val in self.env_vars.get('sim', {}).items():
            if val is not None:
                setattr(self, robot_var_name, val)
            
        self.sim_dir_path = os.path.split(sim_config_file_path)[0]
        define_pathes = ["..\\..\\data\\simulation_data\\default\\map.xml", 
                         "..\\..\\data\\simulation_data\\default\\destinations.xml",
                         "..\\..\\data\\simulation_data\\default\\robots.xml", 
                         "..\\..\\data\\simulation_data\\default\\queue.xml", 
                         "..\\..\\data\\simulation_data\\sim_v0\\dws_conf",
                         ""]
        self.env_run_path = os.path.dirname(os.path.abspath(__file__))
        for i,var_path in enumerate(["MAP_CONFIG_PATH", "DESTINATIONS_CONFIG_PATH", "ROBOT_CONFIG_PATH", "QUEUE_CONFIG_PATH", "DWS_CONFIG_PATH", "WMS_CONFIG_PATH"]):
            if getattr(self, var_path) is not None and getattr(self, var_path) not in define_pathes:
                setattr(self, var_path, os.path.join(self.sim_dir_path, os.path.normpath(getattr(self, var_path))))
            else:
                setattr(self, var_path, os.path.join(self.env_run_path, os.path.normpath(define_pathes[i])))
                
        self.env_ = simpy.Environment()
        self.max_duration = 0
        self.map_ = PostMap(self.env_, self.MAP_CONFIG_PATH, self.DESTINATIONS_CONFIG_PATH, **self.env_vars.get('map', {}))
        self.robot_controller = RobotController(self.env_, self.map_, None, self.QUEUE_CONFIG_PATH, self.ROBOT_CONFIG_PATH, config_vars = self.env_vars)        
        self.wms_ = WMS_communicator(map_controller=self.map_, input_type=self.WMS_CONTROLLER_TYPE, fpath = self.WMS_CONFIG_PATH, server=self.WMS_CONTROLLER_PORT, logpath = self.env_vars.get('logger', dict()).get("LOGGER_OUT_PATH"))
        self.robot_controller.wms_ = self.wms_
        self.dws_ = DWS_communicator(input_type = self.DWS_CONTROLLER_TYPE, fpath = self.DWS_CONFIG_PATH, server=self.DWS_CONTROLLER_PORT) # not actually file but path to dir where shelve is stored
        self.logger_ = Logger(self, **self.env_vars.get('logger', {}))
        self.pre_setup()
        
    def pre_setup(self):
        if self.START_TIME is None:
            self.START_TIME = self.dws_.get_start_date()
        self.current_time = self.START_TIME
        if self.END_TIME is None:
            self.END_TIME = self.dws_.get_end_date() + datetime.timedelta(minutes = 10)
            self.run_mode = "UNTIL_WORK_COMPLETE"
        self.current_tick = 0
        self.number_packages_sent = 0
        self.number_ticks = int(((self.END_TIME - self.START_TIME).total_seconds() + self.ONE_TICK - 1)/self.ONE_TICK)
        self.delta_tick_time = datetime.timedelta(seconds = self.ONE_TICK)
        self.dws_.split_events_by_ticks(self.START_TIME, self.END_TIME, self.ONE_TICK)
        self.logger_.create_obs_start_configs(self)
        
    def run_time_loop(self):
        """ 
        1. get events from dws
        2. send requests to wms to make pathes
        3. update pathes in robot controller
        4. make robot controller loop
        5. log current map state
        """
        self.logger_.log_obs_event(self.robot_controller)
        for event in self.dws_.receive_tick_events(self.current_tick):
            self.robot_controller.process_package(event)
            
        self.robot_controller.make_routine_loop()
        yield self.env_.timeout(1)
        self.current_tick += 1
        self.current_time += self.delta_tick_time   
        
        if self.current_tick%60 == 0:
            print(self.state())
    
    def process_routine(self):
        # setup bar
        bar = tqdm(range(self.number_ticks))       
        
        if self.run_mode == "FIXED_END_TIME" or self.run_mode == "FIXED_NUM_TICKS":
            for i in range(self.number_ticks):
                yield self.env_.process(self.run_time_loop())
                bar.update()
        elif self.run_mode == "UNTIL_ACTIONS_COMPLETE":
            while self.robot_controller.pckg_sent < self.dws_.total_packages:
                yield self.env_.process(self.run_time_loop())
                bar.update()
        else:
            print("Not available yet, try other run_mode")
        
    def run(self, max_duration = None, run_mode = None):
        #here we are supposed to reset state, bur right now we don't have this option
        self.robot_controller.pckg_sent = 0
        
        print("-"*60)
        print(f"{'Sim name: ' + self.NAME:<20} {'Start time: ' + self.START_TIME.strftime('%d/%m/%Y, %H:%M:%S'):<35} {'End time: ' + self.END_TIME.strftime('%d/%m/%Y, %H:%M:%S'):<35}")
        print(f"{'Robots: ' + str(self.robot_controller.number_robots):<20} {'Queues: ' + str(self.robot_controller.number_queues):<20} {'Packages: ' + str(self.dws_.total_packages)}")
        print(f"Simulation started running at {datetime.datetime.now().strftime('%d/%m/%Y, %H:%M:%S')}")
        print("-"*60)
                
        if run_mode is not None:
            assert run_mode in self.run_modes
            self.run_mode = run_mode
        self.max_duration = max_duration
        if self.max_duration is not None:
            self.run_mode = "FIXED_NUM_TICKS"
            self.number_ticks = min(self.number_ticks, max_duration)
        self.env_.process(self.process_routine())
        self.env_.run(until = self.max_duration)
        
        print(self.state())     
        print(f"Simulation stopped running")
        
    def state(self):
        # returns string representing current state  
        line_time = f"{'System time: ' + datetime.datetime.now().strftime('%d/%m/%Y, %H:%M:%S'):<35} {'Sim time: ' + self.current_time.strftime('%d/%m/%Y, %H:%M:%S'):<35} {'Tick: ' + str(self.current_tick)}"
        line_packages = f"{'Packages sent: ' + str(self.robot_controller.pckg_sent):<35} {'Packages to be sent: ' + str(self.dws_.total_packages - self.robot_controller.pckg_sent)}"
        return "-"*len(line_time) + "\n" + line_time + "\n" + line_packages + "\n" + "-"*len(line_time) + "\n"
        
if __name__ == "__main__":
    env_controller = EnvController(sim_config_file_path = r"E:\E\Copy\PyCharm\RoboPost\PostSimulation\data\simulation_data\sim_v1\var_config.json")
    #env_controller.map_.show()
    
    env_controller.run(max_duration = None)