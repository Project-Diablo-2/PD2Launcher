using PD2Shared.Models;
using System.Diagnostics.CodeAnalysis;

namespace PD2Shared.GameFileUpdate.Internal
{
    internal class FileUpdateModelEqualityComparer : IEqualityComparer<FileUpdateModel>
    {
        public bool Equals(FileUpdateModel? x, FileUpdateModel? y)
        {
            return
                x?.Client == y?.Client &&
                x?.FilePath == y?.FilePath &&
                x?.Other == y?.Other;
        }

        public int GetHashCode([DisallowNull] FileUpdateModel obj)
        {
            return HashCode.Combine(obj.Client, obj.FilePath, obj.Other);
        }
    }
}
