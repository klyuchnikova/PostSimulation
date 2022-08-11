import os
import sys
import datetime
import sqlite3
from main.tools.generators import save_map_log_configuration

class Logger:
    def __init__(self, env_controller, LOGGER_OUT_PATH, IS_LOGGING = False, IS_LOGGING_WMS = False, IS_LOGGING_DWS = False, IS_LOGGING_OBSERVATION = False):
        # each of wms, dws, map should be in different files
        # the expected structure:
        #     dir_path  -  {sim_name}_wms_log - wms_log.dat
        #                                       wms_log.bak
        #                                       wms_log.dir        
        #               -  {sim_name}_dws_log - dws_log.dat
        #                                       dws_log.bak
        #                                       dws_log.dir
        #               -  {sim_name}_obs_log.db - SQLLite3 as it's much more effective (i hope)
        self.sim_name = env_controller.NAME
        LOGGER_OUT_PATH = os.path.join(env_controller.sim_dir_path, LOGGER_OUT_PATH)     
        
        self.logging = IS_LOGGING
        self.out_path = LOGGER_OUT_PATH
        
        if not os.path.exists(self.out_path):
            os.makedirs(self.out_path)
        
        DB_PATH = os.path.join(LOGGER_OUT_PATH, f"{self.sim_name}_obs_log.db")
        self.db_path = DB_PATH
        
    def setup_obs_db(self, db_path):
        start_commands = ["""CREATE TABLE IF NOT EXISTS robot_positions(
   id INT PRIMARY KEY,
   frame_id INT,
   robot_id TEXT,
   x TEXT,
   y TEXT,
   d TEXT,
   has_pckg TEXT);
""", """DELETE FROM robot_positions;"""]
        
        self.conn = sqlite3.connect(db_path)
        self.cur = self.conn.cursor()
        for command in start_commands:
            self.cur.execute(command)
            self.conn.commit()
      
    def create_obs_start_configs(self, env_controller):
        map_path = os.path.join(self.out_path, f"{self.sim_name}_obs_map.xml")
        save_map_log_configuration(map_path, {"classes" : env_controller.map_.get_map_classes(), 
                                              "files" : env_controller.map_.get_map_coloring_files(),
                                              "START_TIME" : env_controller.START_TIME,
                                              "END_TIME" : env_controller.END_TIME,
                                              "ONE_TICK" : env_controller.ONE_TICK})
        self.setup_obs_db(self.db_path)
        self.db_id = 0
        self.frame_id = 0
        
    def log_wms_event(self):
        pass
    def log_dws_event(self):
        pass
    def log_obs_event(self, robot_controller):
        logs = zip(range(self.db_id, self.db_id + robot_controller.number_robots), 
                   [self.frame_id]*robot_controller.number_robots, 
                   robot_controller.robot_order2id, 
                   (str(pos[0]) for pos in robot_controller.robot_positions_),
                   (str(pos[1]) for pos in robot_controller.robot_positions_),
                   map(str, robot_controller.robot_directions_),
                   map(str, robot_controller.get_robots_filling()))
        self.cur.executemany("INSERT INTO robot_positions VALUES(?, ?, ?, ?, ?, ?, ?);", logs)
        self.conn.commit()
        
        self.db_id += robot_controller.number_robots
        self.frame_id += 1        
    
    def __del__(self):
        self.cur.close()
        self.conn.close()
        
        
s = os.path.normpath(r"E:\E\Copy\PyCharm\RoboPost\PostSimulationP\data\logs")
s.replace("\\", "\\\\")
print(s)