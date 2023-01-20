using System;
using System.Linq;

namespace Dwapi.SettingsManagement.Core.DTOs
{
    public class AppVerDto
    {
        public string LocalVersion { get; }
        public string LiveVersion { get;  }
        public bool UpdateAvailable => CompareVersions();
        public string HasUpdates => CompareWithLocal() ? "Yes" : "No";

        public AppVerDto(string localVersion, string content)
        {
            LocalVersion = localVersion;
            if (!string.IsNullOrWhiteSpace(content))
            {
                var lines = content.Split(new[] { "\r\n" }, StringSplitOptions.RemoveEmptyEntries).ToList();
                var ver = lines.FirstOrDefault(x => x.Contains("ProductVersion"));
                if (null != ver)
                    LiveVersion = ver.Split("=").LastOrDefault();
            }

        }

        private bool CompareVersions()
        {
            if (string.IsNullOrWhiteSpace(LiveVersion))
            {
                return false;
            }

            if (!string.IsNullOrWhiteSpace(LiveVersion))
            {
                return LiveVersion.Trim() != LocalVersion.Trim();
            }

            return true;
        }
        
        private bool CompareWithLocal()
        {
            if (string.IsNullOrWhiteSpace(LiveVersion))
            {
                return false;
            }

            if (!string.IsNullOrWhiteSpace(LiveVersion))
            {
                int l;
                int c;

                string lv = LiveVersion.Replace(".", "").Trim();
                string cv = LocalVersion.Replace(".", "").Trim();

                Int32.TryParse(lv, out l);
                Int32.TryParse(cv, out c);

                return l > c;
            }

            return true;
        }

        public override string ToString()
        {
            return $"Current:{LiveVersion}, Local:{LocalVersion}, UpdateAvailable:{UpdateAvailable}";
        }
    }
}
