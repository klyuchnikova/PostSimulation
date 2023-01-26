using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SkladModel
{
    [Serializable]
    public class SkladConfig
    {
        public double zoneSize = 1.0; // размер одной зоны в метрах 
        public double unitSize = 1.0; // размер одного робота физический
        public double safeUnitSize = 1.0; // рвзмер зоны робота в которую нельзя заезжать другим роботам
        public double unitSpeed = 3; // Скорость робота
        public double unitAccelerationTime = 0; // Время набора скорости юнита от 0 до UnitSpeed 
        public double unitStopTime = 0; // Время остановки юнита с UnitSpeed до 0
        public double unitRotateTime = 4; //Время разворота юнита на 90 градусов
        public double unitAccelerationEnergy = 10; //Стоимость разгона
        public double unitStopEnergy = 1; // Энергия на остановку
        public double unitMoveEnergy = 1; // Энергия на 1 секунду движения
        public double unitRotateEnergy = 3; // Энергия на разворот
        public double loadTime = 2;  // Время погрузки
        public double unloadTime = 1; // 
        public double unitLoadEnergy = 0.05;
        public double unitUnloadEnergy = 0.05;
        public double unitWaitEnergy = 0.005;
        public double unitChargeTime = 720;
        public double unitChargeValue = 7200;
        public int unitCount = 1;
        public string skladLayout;
        public string antBotLayout;
    }
}
