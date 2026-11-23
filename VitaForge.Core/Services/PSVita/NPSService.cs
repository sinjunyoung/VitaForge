using VitaForge.Core.Models.PSVita;
using System.IO;

namespace VitaForge.Core.Services.PSVita
{
    public class NPSService
    {
        private static readonly Lazy<NPSService> _instance = new(() => new NPSService());

        public static NPSService Instance => _instance.Value;

        private readonly Lock _gate = new();

        private bool _isInitialized = false;

        private NPSService() { }

        public void Initialize()
        {
            lock (_gate)
            {
                if (_isInitialized)
                    return;

                string appPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "PSV_GAMES.tsv");
                string addContPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "PSV_DLCS.tsv");

                try
                {
                    NPSTsvParser.Instance.LoadData(appPath, addContPath);
                }
                catch { }

                _isInitialized = true;
            }
        }

        public NPSGroup? GetGroup(string? titleId)
        {
            Initialize();

            if (string.IsNullOrEmpty(titleId))
                return null;

            NPSTsvParser.Instance.TryGetGroup(titleId, out var group);

            return group;
        }
    }
}