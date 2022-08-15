from main.tools.xml_parser import xmldom_load, xmldom2dict
from xml.dom import minidom
import json
import shelve
import os
from datetime import datetime, date

def load_xml_config_key(fpath, key):
    assert os.path.isfile(fpath)
    tree = xmldom_load(fpath)
    while key not in tree.keys():
        tree = list(tree.values())[0]
    tree = tree[key]
    return tree    

def load_map_configuration(fpath):
    doc = minidom.parse(fpath).getElementsByTagName('map')[0]
    needed_keys = ['mapcells', 'mapcells-attrs', 'stations', 'virtualstations', 'chargers', 'sortingareas', 'destinations', 'sendingareas']
    map_dom = xmldom2dict(doc)['map']
    map_dom['stations'] = [xmldom2dict(elem)['station'] for elem in doc.getElementsByTagName('station')]
    map_dom['virtualstations'] = [xmldom2dict(elem)['virtualstation'] for elem in doc.getElementsByTagName('virtualstation')]
    map_dom['chargers'] = [xmldom2dict(elem)['charger'] for elem in doc.getElementsByTagName('charger')]
    map_dom['destinations'] = [xmldom2dict(elem)['destination'] for elem in doc.getElementsByTagName('destination')]
    map_dom['sortingareas'] = [xmldom2dict(elem)['area'] for elem in doc.getElementsByTagName('area')]
    map_dom['sendingareas'] = [xmldom2dict(elem)['sendingarea'] for elem in doc.getElementsByTagName('sendingarea')]
    return map_dom

def load_robot_configuration(fpath):
    doc = minidom.parse(fpath)
    return [xmldom2dict(robot)['robot'] for robot in doc.getElementsByTagName('robot')]

def load_queue_areas(fpath):
    doc = minidom.parse(fpath)
    data = [xmldom2dict(robot)['queue'] for robot in doc.getElementsByTagName('queue')]
    for queue in data:
        queue['receiver_direction'] = int(queue['receiver_direction'])
        queue['receiver_id'] = int(queue['receiver_id'])
        queue['path'] = list(map(lambda x: tuple(map(int, x.split())), queue['path']))
    return data


def json_from_datetime(obj):
    for key in obj.keys():
        if isinstance(obj[key], dict):
            json_from_datetime(obj[key])
        elif isinstance(obj[key], str):
            try:
                obj[key] = datetime.fromisoformat(obj[key])
            except ValueError:
                pass
    return obj

def load_env_configuration(fpath):
    with open(fpath, 'r') as f:
        return json.load(f, object_hook=json_from_datetime)
        
def load_dws_configuration(fpath):
    # shelve like {date-time in iso : [{"id" : string, "conveyer_id" : int, 'direction': string}, ]
    assert os.path.isdir(fpath)
    fpath = os.path.normpath(fpath)
    fpath = os.path.join(fpath, os.path.split(fpath)[1])
    with shelve.open(fpath) as log_wms_file:
        return dict(map(lambda x: (datetime.fromisoformat(x[0]),x[1]), log_wms_file.items()))
