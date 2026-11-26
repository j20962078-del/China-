using UnityEditor;
using UnityEngine;

namespace ChinaFishing
{
    // Модели удилищ должны быть читаемыми: RodBend гнёт их вершины прямо в игре
    public class RodImportSettings : AssetPostprocessor
    {
        void OnPreprocessModel()
        {
            if (!assetPath.Replace('\\', '/').Contains("/Resources/Rods/")) return;
            var mi = assetImporter as ModelImporter;
            if (mi != null) mi.isReadable = true;
        }
    }
}
