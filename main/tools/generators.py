import json
import os
from dict2xml import dict2xml
from datetime import datetime, timedelta
import random
import shelve
import numpy as np
from main.tools.loaders import load_env_configuration
from main.entities.post_map import PostMap
import simpy

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
        
def delete_shelve_dir(path):
    for file in os.listdir(path):
        os.remove(os.path.join(path, file))
    os.rmdir(path)  

def generate_env_config_file(fpath, sim, Robot):
    vars_d = {"sim" : ["NAME", "START_TIME", "END_TIME", "ONE_TICK", "WMS_CONTROLLER_TYPE", "WMS_SERVER_PORT", "DWS_CONTROLLER_TYPE", "DWS_SERVER_PORT", "MAP_CONFIG_PATH", "ROBOT_CONFIG_PATH", "QUEUE_CONFIG_PATH"],
              "robots" : ["TAKING_PACKAGE_TIMEOUT", "MOVING_ONE_TILE_TIMEOUT", "TURNING_ONE_TIMEOUT", "SENDING_PACKAGE_TIMEOUT"],
              "logger" : ["LOGGER_OUT_PATH", "IS_LOGGING"],
              }
    data = dict([(var_k, dict()) for var_k in vars_d.keys()])
    for var_ in vars_d["sim"]:
        data["sim"][var_] = getattr(sim, var_)
    for var_ in vars_d["robots"]:
        data["robots"][var_] = getattr(Robot, var_)
    for var_ in vars_d["logger"]:
        data["logger"][var_] = getattr(sim.logger_, var_)
    save_env_configuration(fpath, data)
            
def generate_dws_config_file(fpath, start_date, end_date, tick_duration, number_packages, number_conveyers, destinations = None):
    # shelve like {date-time in iso : {"id" : string, "conveyer_id" : int}
    if not os.path.isdir(fpath):
        os.mkdir(fpath)
    else:
        # erase previous
        delete_shelve_dir(fpath)
    fpath = os.path.join(fpath, os.path.basename(fpath))
        
    second_duration = (end_date - start_date).total_seconds()
    package_arrivals = [random.randint(0, second_duration//tick_duration) for i in range(number_packages)]
    package_conveyers = [random.randint(1, number_conveyers) for i in range(number_packages)]
    probs = np.array([*destinations.values()])
    probs/=sum(probs)
    package_destinations = np.random.choice(list(destinations.keys()), size = (number_packages), p = probs)
    
    with shelve.open(fpath) as conf:
        for i in range(number_packages):
            date = start_date + timedelta(seconds = package_arrivals[i]*tick_duration)
            prev = conf.setdefault(date.isoformat(), [])
            prev.append({"id" : "pkg_" + str(i), "conveyer_id" : package_conveyers[i], "destination" : package_destinations[i]})
            conf[date.isoformat()] = prev

def generate_define_dws_config(env_conf_path):
    env_conf_path = os.path.normpath(env_conf_path)
    sim_dir = os.path.dirname(env_conf_path)
    distrib_path = '..\\..\\data\\simulation_data\\default\\destinations_distribution.json'
    with open(distrib_path, 'r') as f:
        dist = json.load(f)["destinations"]
    env_vars = load_env_configuration(env_conf_path)['sim']
    start_time = env_vars["START_TIME"]
    end_time = env_vars.get("END_TIME", start_time + timedelta(hours = 1))
    tick_duration = env_vars["ONE_TICK"]
    generate_dws_config_file(os.path.join(sim_dir, 'dws_conf'), start_date=start_time, end_date=end_time, tick_duration=tick_duration, number_packages=100, number_conveyers=10, destinations=dist)
            
def generate_destination_config(fpath, destination_list = None, places_list = None):
    if destination_list is None or places_list is None:
        with open("..\..\data\simulation_data\default\destinations_distribution.json") as dest_file:
            data = json.load(dest_file)
            destination_list = data["destination_tiles"] 
            places_list = list(data["destinations"].keys())
    destinations = []
    for i, dest_id in enumerate(destination_list):
        destinations.append({"id" : dest_id, "place_id" : places_list[i%len(places_list)]})
    with open(fpath, 'w') as f:
        f.write('<?xml version="1.0" encoding="UTF-8"?>\n' + dict2xml({"destination" : destinations}, wrap = "destinations"))      
     
def generate_random_robot_config_on_free_tiles(map_path, save_path = "robot_v0.xml", number_robots = 5):
    map_ = PostMap(simpy.Environment(), map_file_path = map_path)
    tile_pos = np.random.choice(map_.get_tiles_by_class(TileClass.PATH_TILE), number_robots)
    robot_tiles = [(t.x, t.y) for t in tile_pos]
    data = []
    for i in range(number_robots):
        data.append({'robot_id': "rob_"+str(i), 'x': robot_tiles[i][0] + map_.left_shift_, 'y': robot_tiles[i][1] + map_.up_shift_, 'direction' : random.randint(0, 3)})
    save_robot_configuration(save_path, data)
            
if __name__ == "__main__":
    """
    file_path = r"E:\E\Copy\PyCharm\RoboPost\PostSimulation\data\simulation_data\sim_v0\dws_conf2"
    generate_dws_config_file(os.path.normpath(file_path), datetime.fromisoformat("2011-11-04T09:00:00"), datetime.fromisoformat("2011-11-04T09:10:00"), 60, number_packages = 20, number_conveyers = 10, destinations = [f"D{i}" for i in range(50)])
    with shelve.open(os.path.join(file_path, "dws_conf")) as conf:
        for key in conf.keys():
            print(f"{key} : {conf[key]}")
    #delete_shelve_dir(file_path)
    """
    #generate_define_dws_config(r"E:\E\Copy\PyCharm\RoboPost\PostSimulation\data\simulation_data\sim_v1\var_config.json")