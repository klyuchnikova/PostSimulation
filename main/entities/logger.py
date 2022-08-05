import os
import sys
import datetime
import shelve

class Logger:
    def __init__(self, sim_name, LOGGER_OUT_PATH, IS_LOGGING = False, IS_LOGGING_WMS = False, IS_LOGGING_DWS = False, IS_LOGGING_OBSERVATION = False):
        # each of wms, dws, map should be in different files
        # the expected structure:
        #     dir_path  -  {sim_name}_wms_log - wms_log.dat
        #                                       wms_log.bak
        #                                       wms_log.dir        
        #               -  {sim_name}_dws_log - dws_log.dat
        #                                       dws_log.bak
        #                                       dws_log.dir
        #               -  {sim_name}_obs_log.shelve - first, but then maybe SQLLite3 as it's much more effective (i hope)
        self.logging = IS_LOGGING
        self.out_path = LOGGER_OUT_PATH
        
    def log_wms_event(self):
        pass
    def log_dws_event(self):
        pass
    def log_obs_event(self, robot_controller):
        print(f"log")
        pass