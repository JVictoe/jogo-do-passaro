#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

/// <summary>
/// Unity 6 (v2.1): remaps up to three source materials of each imported FBX to
/// three shared .mat assets, using the FBX importer (not scene Renderer overrides).
/// Applies only to selected model assets/folders, including child folders.
/// </summary>
public sealed class CityModelMaterialsBatch : EditorWindow
{
    private Material material1;
    private Material material2;
    private Material material3;

    private bool applyImportSettings = true;
    private bool replaceExistingRemaps = false;
    private Vector2 scrollPosition;
    private List<string> modelPaths = new List<string>();

    [MenuItem("Tools/City Assets/Assign Shared Materials (Batch)")]
    private static void Open()
    {
        var window = GetWindow<CityModelMaterialsBatch>("City Materials Batch");
        window.minSize = new Vector2(510f, 470f);
        window.RefreshSelection();
        window.Show();
    }

    // Keep the previously used menu name working; it now opens the complete tool.
    [MenuItem("Tools/City Assets/Apply Embedded Materials to Selection")]
    private static void OpenFromOldMenu() => Open();

    private void OnSelectionChange()
    {
        RefreshSelection();
        Repaint();
    }

    private void RefreshSelection()
    {
        var paths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (Object selected in Selection.GetFiltered<Object>(SelectionMode.Assets))
        {
            string selectedPath = AssetDatabase.GetAssetPath(selected);
            if (string.IsNullOrEmpty(selectedPath))
                continue;

            if (AssetDatabase.IsValidFolder(selectedPath))
            {
                foreach (string guid in AssetDatabase.FindAssets(string.Empty, new[] { selectedPath }))
                {
                    string assetPath = AssetDatabase.GUIDToAssetPath(guid);
                    if (AssetImporter.GetAtPath(assetPath) is ModelImporter)
                        paths.Add(assetPath);
                }
            }
            else if (AssetImporter.GetAtPath(selectedPath) is ModelImporter)
            {
                paths.Add(selectedPath);
            }
        }

        modelPaths = paths.OrderBy(path => path, StringComparer.OrdinalIgnoreCase).ToList();
    }

    private void OnGUI()
    {
        EditorGUILayout.Space(8);
        EditorGUILayout.LabelField("MATERIAIS COMPARTILHADOS DA CIDADE", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "v2.1: Arraste os tres materiais .mat, na MESMA ORDEM usada pelos modelos. " +
            "O script associa o 1o material encontrado no FBX ao Material 1, o 2o ao Material 2 etc. " +
            "Modelos com 1 ou 2 materiais utilizam apenas as primeiras posicoes.",
            MessageType.Info);

        material1 = (Material)EditorGUILayout.ObjectField("Material 1 (obrigatorio)", material1, typeof(Material), false);
        material2 = (Material)EditorGUILayout.ObjectField("Material 2", material2, typeof(Material), false);
        material3 = (Material)EditorGUILayout.ObjectField("Material 3", material3, typeof(Material), false);

        EditorGUILayout.Space(4);
        applyImportSettings = EditorGUILayout.ToggleLeft(
            "Tambem configurar Use Embedded Materials e desativar Search Textures Globally",
            applyImportSettings);
        replaceExistingRemaps = EditorGUILayout.ToggleLeft(
            "Substituir remapeamentos de materiais ja existentes (cuidado)",
            replaceExistingRemaps);

        EditorGUILayout.Space(8);
        using (new EditorGUILayout.HorizontalScope())
        {
            EditorGUILayout.LabelField($"Modelos selecionados: {modelPaths.Count}", EditorStyles.boldLabel);
            if (GUILayout.Button("Atualizar selecao", GUILayout.Width(140)))
                RefreshSelection();
        }

        EditorGUILayout.HelpBox(
            "Selecione UMA pasta da cidade na janela Project, ou um UNICO FBX para testar primeiro. " +
            "A operacao nao modifica os outros modelos do projeto.",
            MessageType.None);

        EditorGUILayout.LabelField("Previa da ordem de materiais (ate 8 modelos):", EditorStyles.boldLabel);
        scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition, GUILayout.MinHeight(135f));
        if (modelPaths.Count == 0)
        {
            EditorGUILayout.LabelField("Nenhum modelo selecionado.");
        }
        else
        {
            foreach (string path in modelPaths.Take(8))
            {
                if (!(AssetImporter.GetAtPath(path) is ModelImporter importer))
                    continue;

                var source = GetSourceMaterialIdentifiers(importer);
                string slotNames = source == null || source.Length == 0
                    ? "nenhum material de origem encontrado"
                    : string.Join("  |  ", source.Select((entry, index) =>
                        $"{index + 1}: {entry.name}"));
                EditorGUILayout.LabelField(System.IO.Path.GetFileName(path), EditorStyles.miniBoldLabel);
                EditorGUILayout.LabelField(slotNames, EditorStyles.wordWrappedMiniLabel);
                EditorGUILayout.Space(4);
            }
        }
        EditorGUILayout.EndScrollView();

        EditorGUILayout.Space(8);
        using (new EditorGUI.DisabledScope(modelPaths.Count == 0 || material1 == null))
        {
            if (GUILayout.Button("APLICAR MATERIAIS AOS MODELOS SELECIONADOS", GUILayout.Height(38f)))
                Apply();
        }
    }

    /// <summary>
    /// In Unity 6, ModelImporter.sourceMaterials is an INTERNAL property, so directly
    /// referencing it fails with CS1061. Reflection reads it only if Unity exposes it.
    /// As a fallback, inspect materials actually assigned to the imported FBX prefab,
    /// preserving first-use slot order. This fallback deliberately skips materials
    /// which cannot be associated unambiguously with the source FBX.
    /// </summary>
    private static AssetImporter.SourceAssetIdentifier[] GetSourceMaterialIdentifiers(ModelImporter importer)
    {
        try
        {
            PropertyInfo sourceProperty = typeof(ModelImporter).GetProperty(
                "sourceMaterials",
                BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);

            if (sourceProperty != null)
            {
                var identifiers = sourceProperty.GetValue(importer, null)
                    as AssetImporter.SourceAssetIdentifier[];
                if (identifiers != null && identifiers.Length > 0)
                    return identifiers;
            }
        }
        catch (Exception exception)
        {
            Debug.LogWarning("[City Materials Batch] Nao foi possivel ler a lista " +
                             "interna de materiais de " + importer.assetPath + ": " +
                             exception.Message);
        }

        // Public-API fallback when Unity changes its internal importer implementation.
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(importer.assetPath);
        if (prefab == null)
            return new AssetImporter.SourceAssetIdentifier[0];

        var found = new List<AssetImporter.SourceAssetIdentifier>();
        var names = new HashSet<string>(StringComparer.Ordinal);
        var externalMappings = importer.GetExternalObjectMap();

        foreach (Renderer renderer in prefab.GetComponentsInChildren<Renderer>(true))
        {
            foreach (Material mat in renderer.sharedMaterials)
            {
                if (mat == null)
                    continue;

                string materialPath = AssetDatabase.GetAssetPath(mat);
                AssetImporter.SourceAssetIdentifier identifier;

                if (string.Equals(materialPath, importer.assetPath,
                    StringComparison.OrdinalIgnoreCase))
                {
                    // Imported material sub-asset from this exact FBX.
                    identifier = new AssetImporter.SourceAssetIdentifier(typeof(Material), mat.name);
                }
                else
                {
                    // Already remapped: recover original SOURCE id, not the external name.
                    var matches = externalMappings.Where(entry =>
                        entry.Key.type == typeof(Material) && entry.Value == mat).ToArray();
                    if (matches.Length != 1)
                        continue;
                    identifier = matches[0].Key;
                }

                if (names.Add(identifier.name))
                    found.Add(identifier);
            }
        }

        return found.ToArray();
    }

    private void Apply()
    {
        Material[] targetMaterials = { material1, material2, material3 };

        for (int i = 0; i < targetMaterials.Length; i++)
        {
            if (targetMaterials[i] == null)
                continue;

            string materialPath = AssetDatabase.GetAssetPath(targetMaterials[i]);
            if (string.IsNullOrEmpty(materialPath) ||
                !materialPath.EndsWith(".mat", StringComparison.OrdinalIgnoreCase))
            {
                EditorUtility.DisplayDialog(
                    "Material invalido",
                    $"Material {i + 1} deve ser um arquivo .mat do Project (nao um material temporario ou embutido no FBX).",
                    "OK");
                return;
            }
        }

        if (!EditorUtility.DisplayDialog(
            "Aplicar materiais em lote?",
            $"Serão verificados {modelPaths.Count} modelos.\n\n" +
            "Modelos com mais de 3 materiais, sem materiais de origem identificaveis ou com algum material necessario nao definido serao ignorados.\n\n" +
            "Recomendamos commit/backup antes de executar.\n\n" +
            "Quer continuar?",
            "Aplicar", "Cancelar"))
            return;

        int updatedModels = 0;
        int updatedMappings = 0;
        int unchanged = 0;
        int skipped = 0;
        int errors = 0;
        int index = 0;
        bool cancelled = false;
        var details = new List<string>();

        try
        {
            foreach (string path in modelPaths)
            {
                index++;
                if (EditorUtility.DisplayCancelableProgressBar(
                    "City Materials Batch", path, (float)index / modelPaths.Count))
                {
                    cancelled = true;
                    break;
                }

                try
                {
                    if (!(AssetImporter.GetAtPath(path) is ModelImporter importer))
                        continue;

                    var source = GetSourceMaterialIdentifiers(importer);
                    if (source == null || source.Length == 0 || source.Length > 3)
                    {
                        skipped++;
                        details.Add($"[IGNORADO] {path}: possui {((source == null) ? 0 : source.Length)} materiais de origem.");
                        continue;
                    }

                    // All required slots must be defined before touching this model.
                    int firstMissingIndex = Array.FindIndex(targetMaterials, 0, source.Length, mat => mat == null);
                    if (firstMissingIndex >= 0)
                    {
                        skipped++;
                        details.Add($"[IGNORADO] {path}: falta definir Material {firstMissingIndex + 1}.");
                        continue;
                    }

                    bool changed = false;
                    int localMappings = 0;
                    Dictionary<AssetImporter.SourceAssetIdentifier, Object> existing = importer.GetExternalObjectMap();

                    for (int slot = 0; slot < source.Length; slot++)
                    {
                        var sourceId = source[slot];
                        Material desired = targetMaterials[slot];
                        if (existing.TryGetValue(sourceId, out Object current) && current == desired)
                            continue;
                        if (!replaceExistingRemaps && current != null)
                            continue;

                        importer.AddRemap(sourceId, desired);
                        changed = true;
                        localMappings++;
                    }

                    if (applyImportSettings)
                    {
                        if (importer.materialLocation != ModelImporterMaterialLocation.InPrefab)
                        {
                            importer.materialLocation = ModelImporterMaterialLocation.InPrefab;
                            changed = true;
                        }
                        if (importer.searchTexturesGlobally)
                        {
                            importer.searchTexturesGlobally = false;
                            changed = true;
                        }
                        if (importer.materialImportMode == ModelImporterMaterialImportMode.None)
                        {
                            importer.materialImportMode = ModelImporterMaterialImportMode.ImportViaMaterialDescription;
                            changed = true;
                        }
                    }

                    if (changed)
                    {
                        importer.SaveAndReimport();
                        updatedModels++;
                        updatedMappings += localMappings;
                    }
                    else
                    {
                        unchanged++;
                    }
                }
                catch (Exception ex)
                {
                    errors++;
                    details.Add($"[ERRO] {path}: {ex.Message}");
                    Debug.LogException(ex);
                }
            }
        }
        finally
        {
            EditorUtility.ClearProgressBar();
        }

        string summary =
            $"Atualizados: {updatedModels} modelos\n" +
            $"Vinculos de material: {updatedMappings}\n" +
            $"Ja corretos/preservados: {unchanged}\n" +
            $"Ignorados: {skipped}\n" +
            $"Erros: {errors}" + (cancelled ? "\nProcesso cancelado antes do fim." : "");

        Debug.Log("[City Materials Batch] " + summary.Replace("\n", " | ") +
                  (details.Count > 0 ? "\n" + string.Join("\n", details) : string.Empty));
        EditorUtility.DisplayDialog("City Materials Batch", summary + "\n\nDetalhes no Console.", "OK");
    }
}
#endif
