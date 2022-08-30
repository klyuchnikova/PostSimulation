import sys, os, json
from main.controllers.env_controller import EnvController
from main.tools.loaders import load_env_configuration
from main.tools.generators import generate_define_dws_config, generate_random_robot_config_on_free_tiles
from animator.animator import Animator

"""
run this file with first argument as the name of the command and the rest as the arguments of the command
list of commands:
    command name           |    arguments
    1) simulate            |    path to environment configuration, max duration of simulation in ticks (optional)
    2) generate_robots     |    path to map configuration, number robots, path to output file
    3) generate_dws_config |    path to environment configuration, number of packages
    4) animate             |    path to environment configuration, path to output directory, frames per tick (:=5, optional)
    5) show                |    path to environment configuration

"""

def simulate(fpath, max_duration = None):
    env = EnvController(fpath)
    env.run(max_duration)
  
def show(fpath, output_path = None, mid_frames = 5):
    fpath = os.path.normpath(fpath)
    env_vars = load_env_configuration(fpath)
    sim_name = env_vars["sim"]["NAME"]
    logger_path = env_vars['logger'].get("LOGGER_OUT_PATH", None)
    if logger_path is None:
        print("Error: logging is off for this simulation")
    else: 
        dir_path = os.path.join(fpath, os.path.normpath(logger_path))
        obs_map_name, obs_log_name = f"{sim_name}_obs_map.xml", f"{sim_name}_obs_log.db"
        anim = Animator(os.path.join(dir_path, obs_map_name), os.path.join(dir_path, obs_log_name), mid_frames, True)
        anim.display()
    
def animate(fpath, output_path = None, mid_frames = 5):
    fpath = os.path.normpath(fpath)
    env_vars = load_env_configuration(fpath)
    sim_name = env_vars["sim"]["NAME"]
    logger_path = env_vars['logger'].get("LOGGER_OUT_PATH", None)
    if logger_path is None:
        print("Error: logging is off for this simulation")
    else: 
        dir_path = os.path.join(fpath, os.path.normpath(logger_path))
        obs_map_name, obs_log_name = f"{sim_name}_obs_map.xml", f"{sim_name}_obs_log.db"
        anim = Animator(os.path.join(dir_path, obs_map_name), os.path.join(dir_path, obs_log_name), mid_frames, False)
        if output_path is None:
            output_path = os.path.join(dir_path, f"{sim_name}_anim.gif")
        anim.generate_zip(output_path)
        
def generate_robots(fpath, nmber_robots = 5, output_path = None):
    fpath = os.path.normpath(fpath)
    map_path = load_env_configuration(fpath)["sim"].get("MAP_CONFIG_PATH", "data\\simulation_data\\default\\map.xml")
    if output_path is None:
        output_path = os.path.join(os.path.dirname(fpath),"robots.json")
    generate_random_robot_config_on_free_tiles(map_path, output_path, number_robots=number_robots)

def generate_dws(fpath):
    generate_define_dws_config(fpath)

if __name__ == "__main__":
    command_name = sys.argv[1]
    args = sys.argv[2:]
    if command_name == "simulate":
        simultate(*args)
    elif command_name == "animate":
        animate(*args)
    elif command_name == "show":
        show(*args)
    elif command_name == "generate_robots":
        generate_robots(*args)
    elif command_name == "generate_dws":
        generate_dws(*args)
    else:
        print("command unknown")