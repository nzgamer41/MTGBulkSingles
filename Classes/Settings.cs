using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MTGBulkSingles.Classes
{
    internal class Settings
    {
        public bool printAllVersions { get; set; } = false;
        public bool matchNameExactly { get; set; } = true;
        public bool useDelay { get; set; } = true;
        public bool includeArtCards { get; set; } = false;
        public int delayMilliseconds { get; set; } = 5000;
        public int settingsVersion = 1;

    }
}
