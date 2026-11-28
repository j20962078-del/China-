using UnityEditor;
using UnityEngine;

namespace ChinaFishing
{
    // Модели рыб из Resources/Fish: материалы собираются кодом (FishModels), поэтому встроенные не нужны
    public class FishImportSettings : AssetPostprocessor
    {
        void OnPreprocessModel()
        {
            if (!assetPath.Replace('\\', '/').Contains("/Resources/Fish/")) return;
            var mi = assetImporter as ModelImporter;
            if (mi != null) { mi.isReadable = false; mi.importNormals = ModelImporterNormals.Import; mi.meshCompression = ModelImporterMeshCompression.Off; }
        }
    }
}
