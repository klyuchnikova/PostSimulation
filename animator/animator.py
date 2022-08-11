import sqlite3
from xml.dom import minidom
from main.tools.xml_parser import xmldom2dict
from datetime import datetime, timedelta
import pygame
import os, sys
import math
import time

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
        self.value_row_texts[2] = self.START_TIME.isoformat()
        self.number_frames = int(((self.END_TIME - self.START_TIME).total_seconds() + self.ONE_TICK - 1)//self.ONE_TICK)
    
    def draw(self, frame_id):
        self.value_row_texts[1] = str(frame_id)
        self.value_row_texts[2] = (self.START_TIME + timedelta(seconds = frame_id*self.ONE_TICK)).isoformat()
        
        pygame.draw.rect(self.win, self.bg_color, self.bg)     
        for i in range(self.number_rows):
            self.win.blit(self.labels[i], (10, self.row_height*i + 5))
            self.win.blit(self.font.render(self.value_row_texts[i], True, self.text_color) , (100, self.row_height*i + 5))
                
    
class MapBlock:
    def __init__(self):
        self.wind = None
        self.map_classes = [] # 2d array of tile classes in string format like "TileClass.BLOCK"
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
        
    def pre_setup(self):
        self.map_height, self.map_width = len(self.map_classes), len(self.map_classes[0])
        self.map_pix_height, self.map_pix_width = self.map_height*(self.tile_pix_height + self.border_width) + self.border_width, self.map_width*(self.tile_pix_width + self.border_width) + self.border_width
        self.map_pix_size = (self.map_pix_width, self.map_pix_height)
        
        self.box_size = (self.map_pix_width, self.map_pix_height)
        
        self.map_pictures = list(map(lambda row: list(map(lambda fpath: 
                                                          pygame.transform.scale(pygame.image.load(fpath), (self.tile_pix_width, self.tile_pix_height)), 
                                                          row)), self.map_pictures_pathes))
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

class Aimator:
    def __init__(self, obs_config_path, robot_config_path, mid_frames = 1):
        pygame.init()
        self.clock = pygame.time.Clock()
        
        self.win = None
        self.obs_config_path = obs_config_path
        self.robot_config_path = robot_config_path
        self.mid_frames = mid_frames #one move will be displayed in mid_frames cadres
        self.fps = 0
        self.label_controller = LabelBlock()
        self.map_controller = MapBlock()
        
        self.win_color = (52, 137, 235)
        self.robot_color = (0, 255, 0)
        self.load_start_configs(self.obs_config_path)
        self.load_robot_config(self.robot_config_path)
        
        self.robots_data = []
        
    def load_start_configs(self, obs_config_path):
        """ 
        <obs>
        "classes" : env_controller.map_.get_map_classes(), 
        "files" : env_controller.map_.get_map_coloring_files(),
        "START_TIME" : env_controller.START_TIME,
        "END_TIME" : env_controller.END_TIME,
        "ONE_TICK" : env_controller.ONE_TICK
        </obs>
        """
        self.sim_name = os.path.split(obs_config_path)[1].split("_")[0]
        doc = minidom.parse(obs_config_path).getElementsByTagName("obs")[0]
        for var in ["START_TIME", "END_TIME", "ONE_TICK"]:
            setattr(self.label_controller, var, doc.getElementsByTagName(var)[0].firstChild.nodeValue)
        self.label_controller.START_TIME = datetime.fromisoformat(self.label_controller.START_TIME)
        self.label_controller.END_TIME = datetime.fromisoformat(self.label_controller.END_TIME)
        self.label_controller.ONE_TICK = int(self.label_controller.ONE_TICK)
        self.label_controller.pre_setup(self.sim_name)
        
        self.map_controller.map_classes = []
        for row in doc.getElementsByTagName("classes"):
            self.map_controller.map_classes.append(row.firstChild.nodeValue.split())
        self.map_controller.map_pictures_pathes = []
        for row in doc.getElementsByTagName("files"):
            self.map_controller.map_pictures_pathes.append(row.firstChild.nodeValue.split())    
        self.map_controller.pre_setup()
        
        self.pre_setup()
        
    def load_robot_config(self, robot_conf_path):
        self.conn = sqlite3.connect(robot_conf_path)
        self.cur = self.conn.cursor()     
        
    def close(self, **kwargs):
        self.cur.close()
        self.conn.close()        
       
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
        
        self.number_frames = self.label_controller.number_frames
        self.robot_radius = self.map_controller.tile_pix_height//2*0.8
        self.one_pause = int(60*self.label_controller.ONE_TICK//self.mid_frames)
        
    def get_robot_frame(self, frame_id):
        self.cur.execute(f'SELECT * FROM robot_positions WHERE frame_id={frame_id}')
        return self.cur.fetchall()
    
    def draw_robot(self, robot_id, x_pix, y_pix, d, has_pckg):
        # x, y -> coordinates in PIXELS! of left top corner of the rectangle
        print(f"drawing robot {(robot_id, x_pix, y_pix, d)}")
        center = (x_pix + self.map_controller.tile_pix_width//2, y_pix + self.map_controller.tile_pix_height//2)
        pygame.draw.circle(self.map_controller.win, self.robot_color, center, self.robot_radius)
        if d >= 4:
            d -= 4
        # d -> grads *= 90
        cos_phi = math.cos(d*math.pi/2)
        sin_phi = math.sin(d*math.pi/2)
        # M = [(cos_phi, sin_phi),
        #     (-sin_phi, cos_phi)]
        x,y = 0, self.map_controller.tile_pix_height//2 - 2
        x, y = cos_phi*x + sin_phi*y, -sin_phi*x + cos_phi*y
        pygame.draw.line(self.map_controller.win, (0, 0, 0), center, end_pos = (center[0] + x, center[1] + y) , width = 5)
    
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
        clock = pygame.time.Clock()
        print("frame_id: ", frame_id)
        for mid_frame in range(self.mid_frames):
            self.draw_background(frame_id)
            for writing in new_positions:
                # example: (0, 0, 'rob_0', 5, 15, 1, 'True')
                robot_id, x, y, d, has_package = writing[2:]
                has_package = (has_package == "True")
                x, y = x*self.map_controller.tile_pix_width, y*self.map_controller.tile_pix_height
                old_x, old_y, old_d = self.robots.get(robot_id, (x, y, d))
                k = (mid_frame+1)/self.mid_frames
                if old_d == 3 and d == 0:
                    d = 4
                elif old_d == 0 and d == 3:
                    old_d = 4
                self.draw_robot(robot_id, x*k + old_x*(1-k), y*k + old_y*(1-k), d*k + old_d*(1-k), has_package)
            self.win.blit(self.map_controller.win, self.map_controller.shifts)
            self.draw_update()
            self.clock.tick(self.one_pause/60)
        for writing in new_positions:
            robot_id, x, y, d, has_package = writing[2:] 
            self.robots[robot_id] = x*self.map_controller.tile_pix_width, y*self.map_controller.tile_pix_height, d
        
    def display(self, dpath):
        frame_id = 0
        self.robots = dict()
        while frame_id < 14:#self.number_frames:
            for event in pygame.event.get():
                if event.type == pygame.QUIT:
                    pygame.quit(); sys.exit(); 
            anim.draw(frame_id)
            time.sleep(1)
            frame_id += 1
        while True:
            for event in pygame.event.get():
                if event.type == pygame.QUIT:
                    pygame.quit(); sys.exit();            
            
    def generate_zip(dpath, sim_name):
        pass
    def generate_zip_to_all(dpath):
        pass
    
if __name__ == "__main__":
    anim = Aimator("E:\E\Copy\PyCharm\RoboPost\PostSimulation\data\logs\sim_v0\sim_v0_obs_map.xml", 
                   "E:\E\Copy\PyCharm\RoboPost\PostSimulation\data\logs\sim_v0\sim_v0_obs_log.db", 
                   5)
    anim.display("")