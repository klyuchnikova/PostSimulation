import sqlite3
from xml.dom import minidom
from xml_parser import xmldom2dict
import xml.etree.ElementTree as ET
from datetime import datetime, timedelta
import pygame
from pygame import surfarray
import imageio
import os, sys, glob
import math
import time
from PIL import Image
import numpy as np
import colorsys
from tqdm import tqdm
import re

class RobotState:
    def __init__(self, x, y, is_x_dir, time_p):
        self.x = x
        self.y = y
        self.direct = is_x_dir
        self.time_p = self.time_p
    
    def copy():
        return RobotState(self.x, self.y, self.is_x_dir, self.time_p)

class LogIterator:
    def __init__(self, logs):
        self.it = logs.iter("AntStateChange")
    def next():
        if self.is_end:
            return
        new_command = None
        while True:
            try:
                new_command = self.it.next()
            except:
                self.is_end = True
                break
            if new_command.find("command").text != "EndTask":
                continue
        return RobotState(round(float(new_command.find("xCoordinate").text) + 0.1), 
                          round(float(new_command.find("yCoordinate").text) + 0.1), 
                          new_command.find("isXDirection").text == "true", 
                          strp_pt_time(new_command.find("lastUpdated").text))

def strp_pt_time(pt):
    format_ = "PT((?P<hours>\d+)H)?((?P<minutes>\d+)M)?(?P<seconds>\d+.\d+)"
    match = re.match(format_, time_ex)
    hours = int(match.group("hours")) if match.group("hours") else 0
    minutes = int(match.group("minutes")) if match.group("minutes") else 0
    seconds = float(match.group("seconds")) if match.group("seconds") else 0
    return timedelta(hours = hours, minutes = minutes, seconds = seconds)

def shift_hue(image, hout = None, light_coeff = None, saturation_coeff = None):
    hsv = np.array(image.convert('HSV').getdata())
    if hout is not None:
        hsv[...,0]=hout
    if light_coeff is not None:
        hsv[...,1]= np.round(hsv[...,1]*light_coeff)
    if saturation_coeff is not None:
        hsv[...,2]= np.round(hsv[...,2]*saturation_coeff)
    hsv = np.transpose(hsv.reshape((image.height, image.width, 3)), (1, 0, 2))
    return np.array(Image.fromarray(hsv.astype('uint8'), 'HSV').convert('RGB')) #np.apply_along_axis(lambda pix: colorsys.hsv_to_rgb(*pix), 0, hsv).reshape((image.height, image.width, 3))

class LabelBlock:
    def __init__(self):
        self.win = None
        self.START_TIME = None # datetime
        self.END_TIME = None # datetime
        self.ONE_TICK = None # int (seconds per frame)
        
        self.min_width = 500
        self.row_height = 25
        self.margin_width = 5
        self.shifts = (self.margin_width, self.margin_width)
        
        self.text_size = int(self.row_height*12//16)
        self.font = pygame.font.Font(None, self.text_size)
        self.text_color = (0,0,0)
        self.bg_color = (52, 137, 235)
        
        self.label_row_texts = ["sim_name", "frame", "datetime"]
        self.labels = [self.font.render(txt + ":", True, self.text_color) for txt in self.label_row_texts]
        self.number_rows = len(self.label_row_texts)
        self.value_row_texts = ["None"] * self.number_rows
        
    def pre_setup(self, sim_name):
        self.box_size = (self.min_width, self.row_height * self.number_rows) 
        self.bg = pygame.Rect(0, 0, *self.box_size)
        self.value_row_texts[0] = sim_name
        self.value_row_texts[1] = "0"
        self.value_row_texts[2] = self.START_TIME.strftime("%d/%m/%Y, %H:%M:%S")
        self.number_frames = int(((self.END_TIME - self.START_TIME).total_seconds() + self.ONE_TICK - 1)//self.ONE_TICK)
    
    def draw(self, frame_id):
        self.value_row_texts[1] = str(frame_id)
        self.value_row_texts[2] = (self.START_TIME + timedelta(seconds = frame_id*self.ONE_TICK)).strftime("%d/%m/%Y, %H:%M:%S")
        
        pygame.draw.rect(self.win, self.bg_color, self.bg)     
        for i in range(self.number_rows):
            self.win.blit(self.labels[i], (10, self.row_height*i + 5))
            self.win.blit(self.font.render(self.value_row_texts[i], True, self.text_color) , (100, self.row_height*i + 5))
                
    
class MapBlock:
    def __init__(self, show_tile_direction = False):
        self.wind = None
        self.show_tile_direction = show_tile_direction
        self.map_classes = [] # 2d array of tile classes in string format like "TileState.BLOCK"
        self.map_pictures_pathes = [] # 2d array of tile     
        self.map_width = 0
        self.map_height = 0
        
        self.tile_pix_width = 20
        self.tile_pix_height = 20  
        self.tile_size = (self.tile_pix_width,  self.tile_pix_height)
        self.tile_back_color = "#DCDCDC"
        self.border_color = "#696969"
        self.border_width = 1
        self.display_axes = True
        
        self.margin_width = 5
        self.map_left_shift = self.margin_width
        self.map_up_shift = self.margin_width
        self.shifts = (self.map_left_shift, self.map_up_shift)
        
    def color_surface(self, surface, red, green, blue):
        arr = surfarray.pixels3d(surface)
        arr[:,:,0] = red
        arr[:,:,1] = green
        arr[:,:,2] = blue
        
    def mark(self, x, y):
        self.map_pictures[y][x].set_alpha(128)
        self.map_pictures[y][x].fill((255, 0, 0))
            
    def load_picture(self, fpath, tile_class):
        if not self.show_tile_direction:
            class_colors = {"TileClass.QUEUE_TILE" : (251, 248, 204), "TileClass.SORTING_TILE": (142, 236, 245), "TileClass.STATION_TILE": (253, 228, 207), "TileClass.CHARGE_TILE": (185, 251, 192), "TileClass.PATH_TILE": (234, 248, 251), "TileClass.SENDING_TILE" : (142, 236, 245)}
            if tile_class in class_colors:
                surface = pygame.Surface((self.tile_pix_width, self.tile_pix_height))
                surface.fill(class_colors[tile_class])
                return surface
        
        img = Image.open(fpath).resize((self.tile_pix_width, self.tile_pix_height))
        surface = pygame.Surface((self.tile_pix_width, self.tile_pix_height))
        tile_class_hue = {"TileClass.STATION_TILE" : (120, None, 0.9), "TileClass.SORTING_TILE": (0,), "TileClass.QUEUE_TILE" : (20,), "TileClass.CHARGE_TILE" : (50,), "TileClass.DESTINATION" : (260,), "TileClass.BLOCK" : (None, None, 0.5)}
        hue = tile_class_hue.get(tile_class, (None,))
        surfarray.blit_array(surface, shift_hue(img, *hue))
        return surface
        
    def pre_setup(self):
        print(self.map_classes)
        
        self.map_height, self.map_width = len(self.map_classes), len(self.map_classes[0])
        self.map_pix_height, self.map_pix_width = self.map_height*(self.tile_pix_height + self.border_width) + self.border_width, self.map_width*(self.tile_pix_width + self.border_width) + self.border_width
        self.map_pix_size = (self.map_pix_width, self.map_pix_height)
        
        self.box_size = (self.map_pix_width , self.map_pix_height)
        
        self.tile_with_borders_size = (self.tile_pix_width + self.border_width, self.tile_pix_height + self.border_width)
        self.map_pictures = [[0]*self.map_width for _ in range(self.map_height)]
        for i in range(self.map_height):
            for j in range(self.map_width):
                self.map_pictures[i][j] = self.load_picture(self.map_pictures_pathes[i][j], self.map_classes[i][j])
        self.bg = pygame.Rect(0, 0, self.map_pix_width, self.map_pix_height) #pygame.surface.Surface(self.map_pix_size)
        
    def drawGrid(self):
        blockSize = 20 #Set the size of the grid block
        for y in range(0, self.map_pix_height, self.tile_pix_height + self.border_width):
            for x in range(0, self.map_pix_width, self.tile_pix_width + self.border_width):
                rect = pygame.Rect(x, y, self.tile_pix_width, self.tile_height)
                pygame.draw.rect(self.win, self.tile_border_color, rect, 1) 
                
    def draw_pictured_tiles(self):
        block_size = self.tile_pix_width + self.border_width
        for y in range(self.map_height):
            for x in range(self.map_width):
                #rect = pygame.Rect(x*block_size, y*block_size, self.tile_pix_width, self.tile_height)
                #pygame.draw.rect(self.win, self.border_color, self.map_pictures[x][y], (x*block_size, y*block_size, self.tile_pix_width, self.tile_pix_height), self.border_width*2)
                self.win.blit(self.map_pictures[y][x], (x*block_size, y*block_size))         
        
    def draw(self, frame_id):
        pygame.draw.rect(self.win, self.tile_back_color, self.bg)
        self.draw_pictured_tiles()

class Animator:
    def __init__(self, obs_config_path, mid_frames = 5, show_window = False, show_tile_direction = True):
        if not show_window:
            os.environ["SDL_VIDEODRIVER"] = "dummy"
        pygame.init()
        self.clock = pygame.time.Clock()
        
        self.win = None
        self.obs_config_path = obs_config_path
        self.mid_frames = mid_frames #one move will be displayed in mid_frames cadres
        self.fps = 60 # considering one tick duration = 1 second, mid_frames = 5 this is optional
        self.label_controller = LabelBlock()
        self.map_controller = MapBlock(show_tile_direction)
        
        self.win_color = (52, 137, 235)
        self.robot_color = (0,250,154)
        self.package_color = (199,21,133)
        self.load_start_configs(self.obs_config_path)
        
        self.robots_data = []
        
    def load_start_configs(self, obs_config_path):
        doc = ET.iterparse(obs_config_path)
        for _, el in doc:
            _, _, el.tag = el.tag.rpartition('}') # strip ns       
        doc = doc.root
         
        self.sim_name =  doc.find('lastUpdated').text
        configs = doc.find('sklad').find('skladConfig') 
        
        for var in ["unitRotateTime", "unitSpeed", "loadTime", "unloadTime", "unitSize"]:
            setattr(self, var, float(configs.find(var).text))
            print(f"var {var} = {float(configs.find(var).text)}")
        self.moveTime = self.unitSize / self.unitSpeed
        print(f"var moveTime = {self.moveTime}")
        self.timeHorizon = max(self.moveTime, self.unitRotateTime)
        
        logs = doc.find("logs")
        
        self.label_controller.START_TIME = datetime.fromisoformat("2000-01-01")
        self.label_controller.END_TIME = datetime.fromisoformat("2000-01-01") + timedelta(seconds = int(logs.find("Capacity").text))
        self.label_controller.ONE_TICK = 1.000
        self.label_controller.pre_setup(self.sim_name)
        
        sklad_map = doc.find('sklad').find('skladLayout')
        self.map_controller.map_classes = []
        for row in sklad_map.iter('Item'):
            row_id = int(row.find('Key').text)
            for block in row.find('Value').iter('Item'):
                col_id = int(block.find("Key").text)
                tile_type_id = int(block.find('Value').text)
                if row_id >= len(self.map_controller.map_classes):
                    self.map_controller.map_classes.append([])
                self.map_controller.map_classes[row_id].append(tile_type_id)
                
        folder_name = "tile_type_pictures"
        type_to_path = {0 : f"{folder_name}/NULL_CELL.png", 
                        1 : f"{folder_name}/BLANK.png", 
                        2 : f"{folder_name}/DROPBOX.png", 
                        3 : f"{folder_name}/SHELF_CELL.png", 
                        4 : f"{folder_name}/CHARGE.png"}
        self.map_controller.map_pictures_pathes = [[type_to_path[self.map_controller.map_classes[row_id][col_id]] 
                                                    for col_id in range(len(self.map_controller.map_classes[row_id]))] 
                                                   for row_id in range(len(self.map_controller.map_classes))]
        self.number_frames = int(logs.find("Capacity").text)
        self.map_controller.pre_setup()
        self.load_robot_config(configs.find("antBotLayout").text, logs)
        self.pre_setup()
        
    def load_robot_config(self, robot_config, logs):
        # load robot config!
        # initial positions
        self.number_frames = 6
        self.frame_duration = 1/6
        self.robots = [[] for i in range(number_frames)]
        robot_config = robot_config.split('\\')[-1]
        with open(robot_config, "r") as rob_init:
            for i in range(self.number_frames):
                self.robots_data[i] = [list(map(int, line.split(', '))) for line in rob_init.readlines()]
            
        # first maybe make a class with simular abilities
        # what we need is to iterate on time 
        # when we say next (timedelta)
        # what actually happens is we look forward on self.timeHorizon and if we see that some action was done we look at last state before the action and update mid frames or something.
        
        self.log_iterator = LogIterator(logs)
        self.last_state = self.log_iterator.next()
        
    def next_frames(self, end_time):
        new_frames = []
        while self.last_state.time_p < end_time:
            new_frames.append(self.last_state.copy())
            self.last_state = self.log_iterator.next()
        return new_frames
        
    def close(self, **kwargs):
        #self.conn.close()
        pass
       
    def pre_setup(self):
        self.map_area_size = (self.map_controller.box_size[0] + self.map_controller.margin_width*2, self.map_controller.box_size[1] + self.map_controller.margin_width*2)
        self.label_area_size = (self.label_controller.box_size[0] + self.label_controller.margin_width*2, self.label_controller.box_size[1] + self.label_controller.margin_width*2)
        self.box_size = (max(self.map_area_size[0], self.label_area_size[0]), 
                         self.map_area_size[1] + self.label_area_size[1])
        self.label_controller.shifts = (self.label_controller.shifts[0], self.label_controller.shifts[1] + self.map_area_size[1])
        # centering
        self.label_controller.shifts = ((self.box_size[0] - self.label_controller.box_size[0])//2, self.label_controller.shifts[1])
        self.map_controller.shifts = ((self.box_size[0] - self.map_controller.box_size[0])//2, self.map_controller.shifts[1])
        
        pygame.display.set_caption(self.sim_name)
        self.win = pygame.display.set_mode(self.box_size)
        self.win.fill((self.win_color))
        
        self.map_controller.win = pygame.Surface(self.map_controller.box_size)
        self.label_controller.win = pygame.Surface(self.label_controller.box_size)
        
        self.robot_radius = self.map_controller.tile_pix_height//2*0.8
        self.one_pause = self.label_controller.ONE_TICK/self.mid_frames
        self.fps = int(self.fps/5*self.label_controller.ONE_TICK*self.mid_frames)
        
    def get_robot_frame(self, frame_id):
        return self.robots_data[frame_id]
    
    def draw_robot(self, robot_id, x_pix, y_pix, d, has_pckg):
        # x, y -> coordinates in PIXELS! of left top corner of the rectangle
        #print(f"drawing robot {(robot_id, x_pix, y_pix, d)}")
        center = (x_pix + self.map_controller.tile_with_borders_size[0]//2, y_pix + self.map_controller.tile_with_borders_size[1]//2)
        pygame.draw.circle(self.map_controller.win, (0, 0, 0), center, self.robot_radius + 2)
        pygame.draw.circle(self.map_controller.win, self.robot_color, center, self.robot_radius)
        if d >= 4:
            d -= 4
        # d -> grads *= 90
        cos_phi = math.cos(d*math.pi/2)
        sin_phi = math.sin(d*math.pi/2)
        # M = [(cos_phi, sin_phi),
        #     (-sin_phi, cos_phi)]
        x, y = 0, -(self.map_controller.tile_with_borders_size[0]//2 - 2)
        x, y = cos_phi*x - sin_phi*y, sin_phi*x + cos_phi*y
        pygame.draw.line(self.map_controller.win, (0, 0, 0), center, end_pos = (center[0] + x, center[1] + y) , width = 5)
        if has_pckg:
            pygame.draw.circle(self.map_controller.win, (0, 0, 0), center, self.robot_radius//2 + 2)
            pygame.draw.circle(self.map_controller.win, self.package_color, center, self.robot_radius//2)
    
    def draw_background(self, frame_id):
        self.map_controller.draw(frame_id)
        self.label_controller.draw(frame_id)
        self.win.blit(self.map_controller.win, self.map_controller.shifts)
        self.win.blit(self.label_controller.win, self.label_controller.shifts)         
    
    def draw_update(self):
        pygame.display.update() 
    
    def draw(self, frame_id):
        # we have previous positions in dict (robot id to position) and therefore need to make a few cadres
        # to do that we need to find those which locations have changed
        new_positions = self.get_robot_frame(frame_id)
        if len(new_positions) == 0:
            self.number_frames = frame_id
            return
        clock = pygame.time.Clock()
        for mid_frame in range(self.mid_frames):
            self.draw_background(frame_id)
            for writing in new_positions:
                # example: (0, 0, 'rob_0', '5', '15', '1', 'True')
                robot_id, x, y, d, has_package = writing[2:]
                x, y, d = int(x), int(y), int(d)
                has_package = (has_package == "True")
                x, y = x*self.map_controller.tile_with_borders_size[0], y*self.map_controller.tile_with_borders_size[1]
                old_x, old_y, old_d = self.robots.get(robot_id, (x, y, d))
                k = (mid_frame+1)/self.mid_frames
                if old_d == 3 and d == 0:
                    d = 4
                elif old_d == 0 and d == 3:
                    old_d = 4
                self.draw_robot(robot_id, x*k + old_x*(1-k), y*k + old_y*(1-k), d*k + old_d*(1-k), has_package)
            self.win.blit(self.map_controller.win, self.map_controller.shifts)
            self.draw_update()
            self.clock.tick(3600/self.fps)
        for writing in new_positions:
            robot_id, x, y, d, has_package = writing[2:]
            x, y, d = int(x), int(y), int(d)
            self.robots[robot_id] = x*self.map_controller.tile_with_borders_size[0], y*self.map_controller.tile_with_borders_size[1], d
        
    def show(self):
        while True:
            for event in pygame.event.get():
                if event.type == pygame.QUIT:
                    pygame.quit(); sys.exit(); 
            self.draw_background(0) 
            self.draw_update()
            self.clock.tick(1)
            
    def display(self):
        frame_id = 0
        self.robots = dict()
        while frame_id < self.number_frames:
            for event in pygame.event.get():
                if event.type == pygame.QUIT:
                    pygame.quit(); sys.exit(); 
            self.draw(frame_id)
            #time.sleep(0.1)
            frame_id += 1
        self.robots = dict()
        while True:
            for event in pygame.event.get():
                if event.type == pygame.QUIT:
                    pygame.quit(); sys.exit();    
    
    def draw_zip(self, frame_id, dpath, cadre):
        # we have previous positions in dict (robot id to position) and therefore need to make a few cadres
        # to do that we need to find those which locations have changed
        new_positions = self.get_robot_frame(frame_id)
        
        if len(new_positions) == 0:
            self.number_frames = frame_id
            return        
        
        for mid_frame in range(self.mid_frames):
            self.draw_background(frame_id)
            if len(new_positions) == 0:
                self.number_frames = frame_id + 1
                # cause it's the last frame with data
            for writing in new_positions:
                # example: (0, 0, 'rob_0', 5, 15, 1, 'True')
                robot_id, x, y, d, has_package = writing[2:]
                x, y, d = int(x), int(y), int(d)
                has_package = (has_package == "True")
                x, y = x*self.map_controller.tile_with_borders_size[0], y*self.map_controller.tile_with_borders_size[1]
                old_x, old_y, old_d = self.robots.get(robot_id, (x, y, d))
                k = (mid_frame+1)/self.mid_frames
                if old_d == 3 and d == 0:
                    d = 4
                elif old_d == 0 and d == 3:
                    old_d = 4
                self.draw_robot(robot_id, x*k + old_x*(1-k), y*k + old_y*(1-k), d*k + old_d*(1-k), has_package)
            self.win.blit(self.map_controller.win, self.map_controller.shifts)
            self.draw_update()
            
            fpath = os.path.join(dpath, f"screen_{cadre}.png")
            pygame.image.save(self.win, fpath)
            self.images.append(imageio.imread(fpath))
            
            cadre += 1
        for writing in new_positions:
            robot_id, x, y, d, has_package = writing[2:]
            x,y,d = int(x), int(y), int(d)
            self.robots[robot_id] = x*self.map_controller.tile_with_borders_size[0], y*self.map_controller.tile_with_borders_size[1], d   
        return cadre
            
    def generate_zip(self, fpath, max_frames = None):
        print(f"{datetime.now().strftime('%H:%M:%S')}: generating frames")        
        fpath = os.path.normpath(fpath)
        dpath = os.path.join(os.path.dirname(fpath), "tmp")
        if not os.path.exists(dpath):
            os.mkdir(dpath)
        
        if max_frames is not None:
            self.number_frames = min(self.number_frames, max_frames)
        frame_id = 0
        cadre = 0
        self.robots = []
        self.images = []
        bar = tqdm(range(self.number_frames))
        while frame_id < self.number_frames:
            cadre = self.draw_zip(frame_id, dpath, cadre)
            bar.update()
            frame_id += 1
        print(f"\n{datetime.now().strftime('%H:%M:%S')}: started generating zip")
        imageio.mimsave(fpath, self.images, fps=self.fps)
        print(f"{datetime.now().strftime('%H:%M:%S')}: result saved to {fpath}")
        self.robots = dict()
        self.images = []
        
        for f in glob.glob(f'{dpath}/*'):
            os.remove(f)
        os.rmdir(dpath)
        pygame.quit(); sys.exit(); 
        
    def generate_zip_to_all(self, dpath):
        pass
    
    def save_picture(self, fpath):
        fpath = os.path.normpath(fpath)
        self.draw_background(0) 
        self.draw_update()
        pygame.image.save(self.win, fpath)
        pygame.quit()
    
    def mark(self, x, y):
        self.map_controller.mark(x, y)
    
if __name__ == "__main__":
    obs_config_path = "..\\log.xml"
    #doc = minidom.parse(obs_config_path).getElementsByTagName("SkladLogger")[0]
    anim = Animator(obs_config_path, 
                       5, True, 
                       show_tile_direction = True)  
    anim.show()
    """
    print(f"elements in sklad: {doc.elements}")
    print(f"elements in sklad: {len(doc.elements)}")
    """
    
    #anim = Animator("E:\E\Copy\PyCharm\RoboPost\PostSimulation\data\logs\sim_v1\sim_v1_obs_map.xml", 
    #               "E:\E\Copy\PyCharm\RoboPost\PostSimulation\data\logs\sim_v1\sim_v1_obs_log.db", 
    #               5, False, 
    #               show_tile_direction = True)
    #anim.mark(9, 0)
    #anim.mark(10, 33)
    #anim.show()
    #anim.display()
    #anim.generate_zip("E:\E\Copy\PyCharm\RoboPost\PostSimulation\data\logs\sim_v1\sim_v1.gif", 300)