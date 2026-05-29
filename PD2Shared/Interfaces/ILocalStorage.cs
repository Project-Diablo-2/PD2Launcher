

using PD2Shared.Models;

namespace PD2Shared.Interfaces
{
        public interface ILocalStorage
        {
            // load everything
            AllSettings Load();

            //save a setting bucket by keyname
            void Update<T>(StorageKey key, T value) where T : class;

            T? LoadSectionIfExists<T>(StorageKey key) where T : class;

            //load a setting bucket by keyname
            T LoadSection<T>(StorageKey key) where T : class, new();

            void InitializeIfNotExists<T>(StorageKey key, T defaultValue) where T : class, new();
        }
    }