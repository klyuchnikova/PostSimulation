import json
import os
from dict2xml import dict2xml
from datetime import datetime, timedelta
import random
import shelve
import numpy as np

def json_serial(obj):
    if isinstance(obj, (datetime)):
        return obj.isoformat()
    raise TypeError ("Type %s not serializable" % type(obj))
def save_env_configuration(fpath, data):
    with open(fpath, 'w') as f:
        json.dump(data, f, indent = 4, default=json_serial)
        
def save_robot_configuration(fpath, data):
    with open(fpath, 'w') as f:
        f.write('<?xml version="1.0" encoding="UTF-8"?>\n' + dict2xml({"robot" : data}, wrap = "robots"))
        
def save_queue_config(fpath, data):
    # list of {"receiver_id" : int, "receiver_direction" : int, "path" : [(x1, y1), (x2, y2) ... ]}
    with open(fpath, 'w') as f:
        f.write('<?xml version="1.0" encoding="UTF-8"?>\n' + dict2xml({"queue" : data}, wrap = "queues"))  

def save_map_log_configuration(fpath, data):
    with open(fpath, 'w') as f:
        f.write('<?xml version="1.0" encoding="UTF-8"?>\n' + dict2xml(data, wrap = "obs"))    

def generate_env_config_file(fpath, sim, Robot):
    vars_d = {"sim" : ["NAME", "START_TIME", "END_TIME", "ONE_TICK", "WMS_CONTROLLER_TYPE", "WMS_SERVER_PORT", "DWS_CONTROLLER_TYPE", "DWS_SERVER_PORT", "MAP_CONFIG_PATH", "ROBOT_CONFIG_PATH", "QUEUE_CONFIG_PATH"],
              "robots" : ["TAKING_PACKAGE_TIMEOUT", "MOVING_ONE_TILE_TIMEOUT", "TURNING_ONE_TIMEOUT", "SENDING_PACKAGE_TIMEOUT"],
              "logger" : ["LOGGER_OUT_PATH", "IS_LOGGING"],
              }
    data = dict([(var_k, dict()) for var_k in vars_d.keys()])
    for var_ in vars_d["sim"]:
        data["sim"][var_] = sim.__dict__[var_]
    for var_ in vars_d["robots"]:
        data["robots"][var_] = Robot.__dict__[var_]
    for var_ in vars_d["logger"]:
        data["logger"][var_] = sim.logger_.__dict__[var_]
    save_env_configuration(fpath, data)
    

def generate_dws_config_file(fpath, start_date, end_date, tick_duration, number_packages, number_conveyers, destinations, dist = "puasson"):
    # shelve like {date-time in iso : {"id" : string, "conveyer_id" : int}
    second_duration = (end_date - start_date).total_seconds()
    package_arrivals = [random.randrange(0, second_duration//tick_duration, 1) for i in range(number_packages)]
    package_conveyers = [random.randrange(0, number_conveyers, 1) for i in range(number_packages)]
    if not os.path.isdir(fpath):
        os.mkdir(fpath)
    fpath = os.path.join(fpath, os.path.basename(fpath))
    with shelve.open(fpath) as conf:
        for i in range(number_packages):
            date = start_date + timedelta(seconds = package_arrivals[i]*tick_duration)
            prev = conf.setdefault(date.isoformat(), [])
            prev.append({"id" : "pkg_" + str(i), "conveyer_id" : package_conveyers[i], "destination" : np.random.choice(destinations)})
            conf[date.isoformat()] = prev
            
def delete_shelve_dir(path):
    for file in os.listdir(path):
        os.remove(os.path.join(path, file))
    os.rmdir(path)    
            
if __name__ == "__main__":
    file_path = r"E:\E\Copy\PyCharm\RoboPost\PostSimulation\data\simulation_data\sim_v0\dws_conf2"
    generate_dws_config_file(os.path.normpath(file_path), datetime.fromisoformat("2011-11-04"), datetime.fromisoformat("2012-01-04"), 60, number_packages = 20, number_conveyers = 8, destinations = [f"D{i}" for i in range(50)])
    with shelve.open(os.path.join(file_path, "dws_conf")) as conf:
        for key in conf.keys():
            print(f"{key} : {conf[key]}")
    delete_shelve_dir(file_path)