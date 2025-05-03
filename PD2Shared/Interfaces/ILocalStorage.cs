

using PD2Shared.Models;

namespace PD2Shared.Interfaces
{
        public interface ILocalStorage
        {
            // load everything
            AllSettings Load();

            //save a setting bucket by keyname
            void Update<T>(StorageKey key, T value) where T : class;

            //load a setting bucket by keyname
            T LoadSection<T>(StorageKey key) where T : class;

            void InitializeIfNotExists<T>(StorageKey key, T defaultValue) where T : class, new();
        }
    }