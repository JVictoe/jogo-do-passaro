// Bird Wing + Head Pose Capture v0.4
// Unity 6 | Coloque em Assets/Editor/BirdWingPoseCapture.cs
// Poses INDEPENDENTES para GroundIdle / Walk / Run.
// Sobrescreve apenas rotacao das asas e ajusta cabeca (rotacao + posicao)
// nos novos clips. Todas as curvas das pernas / corpo ficam preservadas.
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Scripts.Gameplay.Bird.Editor
{
    public sealed class BirdWingPoseCapture : EditorWindow
    {
        [Serializable]
        private class BonePose
        {
            public string name;
            public string path;
            public Quaternion rotation;
            public Vector3 position;
        }

        [Serializable]
        private class ClipPose
        {
            public bool captured;
            public List<BonePose> wings = new List<BonePose>();
            public BonePose head = new BonePose();
            [Range(0f, 3f)] public float wingSway;
        }

        [SerializeField] private GameObject model;
        [SerializeField] private AnimationClip idle;
        [SerializeField] private AnimationClip walk;
        [SerializeField] private AnimationClip run;
        [SerializeField] private ClipPose idlePose = new ClipPose();
        [SerializeField] private ClipPose walkPose = new ClipPose();
        [SerializeField] private ClipPose runPose = new ClipPose();
        [SerializeField] private Vector2 scroll;

        private static readonly string[] RotationProperties =
        {
            "m_LocalRotation.x", "m_LocalRotation.y",
            "m_LocalRotation.z", "m_LocalRotation.w"
        };
        private static readonly string[] PositionProperties =
        {
            "m_LocalPosition.x", "m_LocalPosition.y", "m_LocalPosition.z"
        };

        [MenuItem("Tools/Bird Game/Wing + Head Pose Capture v0.4")]
        private static void Open()
        {
            var window = GetWindow<BirdWingPoseCapture>("Bird Poses v0.4");
            window.minSize = new Vector2(470, 490);
            if (Selection.activeGameObject != null)
                window.model = Selection.activeGameObject;
        }

        private void OnGUI()
        {
            scroll = EditorGUILayout.BeginScrollView(scroll);
            EditorGUILayout.LabelField("Bird Poses independentes - v0.4", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Use UMA copia do Bird.fbx na cena, com Animator desativado. " +
                "Ajuste asas E cabeca (rotacao e posicao), capture a pose para Walk; " +
                "mude a pose e capture para Run. Pode fazer o mesmo para Idle. " +
                "As poses ficam guardadas separadamente.", MessageType.Info);

            model = (GameObject)EditorGUILayout.ObjectField(
                "Copia do Bird na cena", model, typeof(GameObject), true);
            EditorGUILayout.Space(6);
            EditorGUILayout.LabelField("Clips .anim que JA funcionam", EditorStyles.boldLabel);
            idle = (AnimationClip)EditorGUILayout.ObjectField("GroundIdle", idle, typeof(AnimationClip), false);
            walk = (AnimationClip)EditorGUILayout.ObjectField("Walk", walk, typeof(AnimationClip), false);
            run = (AnimationClip)EditorGUILayout.ObjectField("Run", run, typeof(AnimationClip), false);

            EditorGUILayout.Space(8);
            DrawClip("GroundIdle", idle, idlePose);
            DrawClip("Walk", walk, walkPose);
            DrawClip("Run", run, runPose);

            EditorGUILayout.Space(10);
            bool ready = idlePose.captured && walkPose.captured && runPose.captured &&
                idle != null && walk != null && run != null;
            using (new EditorGUI.DisabledScope(!ready || EditorApplication.isPlayingOrWillChangePlaymode))
            {
                if (GUILayout.Button("GERAR 3 CLIPS COM POSES INDEPENDENTES", GUILayout.Height(38)))
                    GenerateAll();
            }
            EditorGUILayout.HelpBox(
                "A geracao cria NOVOS arquivos *_Pose_v04.anim e nao altera " +
                "os clips originais. As curvas de pernas e corpo sao preservadas; " +
                "o movimento existente da cabeca e mantido como variacao " +
                "em torno da pose capturada.", MessageType.None);
            EditorGUILayout.EndScrollView();
        }

        private void DrawClip(string label, AnimationClip source, ClipPose slot)
        {
            EditorGUILayout.BeginVertical("box");
            EditorGUILayout.LabelField(label + (slot.captured ? "  [POSE CAPTURADA]" : "  [PENDENTE]"),
                EditorStyles.boldLabel);
            slot.wingSway = EditorGUILayout.Slider("Balanco das asas (graus)",
                slot.wingSway, 0f, 3f);
            using (new EditorGUI.DisabledScope(
                model == null || source == null || EditorApplication.isPlayingOrWillChangePlaymode))
            {
                if (GUILayout.Button("Capturar asas + cabeca para " + label))
                    Capture(slot, source, label);
            }
            using (new EditorGUI.DisabledScope(model == null || !slot.captured ||
                       EditorApplication.isPlayingOrWillChangePlaymode))
            {
                if (GUILayout.Button("Reaplicar pose salva na copia da cena"))
                    ApplyCapturedPoseToScene(slot);
            }
            using (new EditorGUI.DisabledScope(!slot.captured || source == null ||
                       EditorApplication.isPlayingOrWillChangePlaymode))
            {
                if (GUILayout.Button("Gerar apenas " + label))
                    GenerateOne(source, slot);
            }
            EditorGUILayout.EndVertical();
        }

        private bool TryGetAnimator(out Animator animator, out string error)
        {
            animator = null;
            error = string.Empty;
            if (model == null || !model.scene.IsValid())
            {
                error = "Selecione uma instancia do Bird.fbx na CENA (nao um asset do Project).";
                return false;
            }
            animator = model.GetComponent<Animator>();
            if (animator == null)
                animator = model.GetComponentInChildren<Animator>(true);
            if (animator == null)
            {
                error = "Animator nao encontrado na copia do modelo.";
                return false;
            }
            if (animator.enabled)
            {
                error = "Desative o Animator da COPIA na cena antes de capturar a pose.";
                return false;
            }
            return true;
        }

        private void Capture(ClipPose slot, AnimationClip source, string label)
        {
            if (!TryGetAnimator(out Animator animator, out string error))
            {
                EditorUtility.DisplayDialog("Bird Pose v0.4", error, "OK");
                return;
            }
            if (!IsWritableSource(source, out error))
            {
                EditorUtility.DisplayDialog("Bird Pose v0.4", error, "OK");
                return;
            }

            var wingBones = new List<BonePose>();
            BonePose headBone = null;
            bool leftRoot = false;
            bool rightRoot = false;

            foreach (Transform bone in animator.GetComponentsInChildren<Transform>(true))
            {
                string path = AnimationUtility.CalculateTransformPath(bone, animator.transform);
                if (bone.name == "head")
                    headBone = CreatePose(bone, path);

                bool wing = bone.name.StartsWith("wing_L_", StringComparison.Ordinal) ||
                            bone.name.StartsWith("wing_R_", StringComparison.Ordinal);
                if (!wing) continue;
                wingBones.Add(CreatePose(bone, path));
                if (bone.name == "wing_L_1_1") leftRoot = true;
                if (bone.name == "wing_R_1_1") rightRoot = true;
            }
            if (headBone == null || !leftRoot || !rightRoot)
            {
                EditorUtility.DisplayDialog("Bird Pose v0.4",
                    "Nao localizei 'head', 'wing_L_1_1' e 'wing_R_1_1' no rig. " +
                    "Confira se selecionou o Bird.fbx original.", "OK");
                return;
            }

            // Evita gerar clips silenciosamente incompativeis com a hierarquia.
            bool hasMatchedWing = false;
            foreach (BonePose wing in wingBones)
            {
                if (GetChannel(source, wing.path, RotationProperties[0]) != null)
                {
                    hasMatchedWing = true;
                    break;
                }
            }
            if (!hasMatchedWing || GetChannel(source, headBone.path, RotationProperties[0]) == null)
            {
                EditorUtility.DisplayDialog("Bird Pose v0.4",
                    "O clip '" + source.name + "' nao possui curvas correspondentes " +
                    "as asas/cabeca desta instancia. Confirme que a hierarquia " +
                    "do Animator e a mesma usada para gerar os .anim.", "OK");
                return;
            }

            // Grava snapshot: mudar a cena DEPOIS nao muda esta pose.
            slot.wings = wingBones;
            slot.head = headBone;
            slot.captured = true;
            Repaint();
            Debug.Log("[Bird Pose v0.4] Pose " + label + " guardada: " +
                wingBones.Count + " ossos de asa + cabeca.", model);
        }

        private static BonePose CreatePose(Transform t, string path)
        {
            return new BonePose
            {
                name = t.name, path = path,
                rotation = t.localRotation,
                position = t.localPosition
            };
        }

        private void ApplyCapturedPoseToScene(ClipPose pose)
        {
            if (!TryGetAnimator(out Animator animator, out string error))
            {
                EditorUtility.DisplayDialog("Bird Pose v0.4", error, "OK");
                return;
            }
            var map = new Dictionary<string, Transform>();
            foreach (Transform t in animator.GetComponentsInChildren<Transform>(true))
                map[AnimationUtility.CalculateTransformPath(t, animator.transform)] = t;

            foreach (BonePose bone in pose.wings)
            {
                if (!map.TryGetValue(bone.path, out Transform target)) continue;
                Undo.RecordObject(target, "Apply bird wing pose");
                target.localRotation = bone.rotation;
                EditorUtility.SetDirty(target);
            }
            if (pose.head != null && map.TryGetValue(pose.head.path, out Transform headTransform))
            {
                Undo.RecordObject(headTransform, "Apply bird head pose");
                headTransform.localRotation = pose.head.rotation;
                headTransform.localPosition = pose.head.position;
                EditorUtility.SetDirty(headTransform);
            }
            SceneView.RepaintAll();
        }

        private static bool IsWritableSource(AnimationClip clip, out string error)
        {
            error = "";
            if (clip == null)
            {
                error = "Arraste um arquivo .anim para o campo do clip.";
                return false;
            }
            string path = AssetDatabase.GetAssetPath(clip);
            if (string.IsNullOrEmpty(path) ||
                !path.EndsWith(".anim", StringComparison.OrdinalIgnoreCase))
            {
                error = "Use os .anim gerados, nao os clips embutidos em FBX: " + clip.name;
                return false;
            }
            return true;
        }

        private void GenerateAll()
        {
            if (!GenerateOne(idle, idlePose)) return;
            if (!GenerateOne(walk, walkPose)) return;
            GenerateOne(run, runPose);
        }

        private bool GenerateOne(AnimationClip source, ClipPose pose)
        {
            if (!pose.captured || pose.head == null || pose.wings == null || pose.wings.Count == 0)
            {
                EditorUtility.DisplayDialog("Bird Pose v0.4", "Capture a pose deste clip primeiro.", "OK");
                return false;
            }
            if (!IsWritableSource(source, out string error))
            {
                EditorUtility.DisplayDialog("Bird Pose v0.4", error, "OK");
                return false;
            }

            string sourcePath = AssetDatabase.GetAssetPath(source);
            string dir = Path.GetDirectoryName(sourcePath).Replace('\\', '/');
            string outputPath = AssetDatabase.GenerateUniqueAssetPath(
                dir + "/" + source.name + "_Pose_v04.anim");
            AnimationClip output = null;
            try
            {
                output = Instantiate(source);
                output.name = Path.GetFileNameWithoutExtension(outputPath);
                AssetDatabase.CreateAsset(output, outputPath);
                WriteWings(output, pose.wings, pose.wingSway);
                WriteHead(output, source, pose.head);
                output.EnsureQuaternionContinuity();
                EditorUtility.SetDirty(output);
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
                Debug.Log("[Bird Pose v0.4] Novo clip: " + outputPath);
                return true;
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
                // Se a criacao do asset falhar no meio, o original continua intacto.
                EditorUtility.DisplayDialog("Bird Pose v0.4",
                    "Falha ao gerar '" + source.name + "'. Veja o Console.", "OK");
                return false;
            }
        }

        private static void WriteWings(AnimationClip clip, List<BonePose> bones, float sway)
        {
            float duration = Mathf.Max(0.01f, clip.length);
            const int samples = 40;
            foreach (BonePose bone in bones)
            {
                bool root = bone.name == "wing_L_1_1" || bone.name == "wing_R_1_1";
                bool animated = root && sway > 0.001f;
                int steps = animated ? samples : 1;
                var curves = NewCurves(4);
                Quaternion previous = bone.rotation;
                float side = bone.name.StartsWith("wing_L_", StringComparison.Ordinal) ? 1f : -1f;
                for (int i = 0; i <= steps; i++)
                {
                    float t = duration * i / steps;
                    float wave = Mathf.Sin(2f * Mathf.PI * i / steps);
                    Quaternion q = bone.rotation;
                    if (animated)
                        q *= Quaternion.AngleAxis(side * sway * wave, Vector3.forward);
                    q = Continuous(previous, q);
                    previous = q;
                    AddRotation(curves, t, q);
                }
                WriteRotation(clip, bone.path, curves);
            }
        }

        // Ajusta a pose base da cabeca sem destruir o "head bob" ja existente.
        // No tempo 0 a cabeca tera exatamente a pose capturada na cena.
        // Nos tempos seguintes mantem variacoes da curva original.
        private static void WriteHead(AnimationClip output, AnimationClip source, BonePose head)
        {
            var originalRot = ReadChannels(source, head.path, RotationProperties);
            var originalPos = ReadChannels(source, head.path, PositionProperties);
            float duration = Mathf.Max(0.01f, source.length);
            const int samples = 48;

            Quaternion startRot = originalRot != null
                ? ReadRotation(originalRot, 0f) : head.rotation;
            Vector3 startPos = originalPos != null
                ? ReadPosition(originalPos, 0f) : head.position;
            Quaternion shift = head.rotation * Quaternion.Inverse(startRot);
            var rotCurves = NewCurves(4);
            var posCurves = NewCurves(3);
            Quaternion previous = head.rotation;

            for (int i = 0; i <= samples; i++)
            {
                float t = duration * i / samples;
                Quaternion original = originalRot != null
                    ? ReadRotation(originalRot, t) : startRot;
                Quaternion next = Continuous(previous, shift * original);
                previous = next;
                AddRotation(rotCurves, t, next);

                Vector3 originalPosition = originalPos != null
                    ? ReadPosition(originalPos, t) : startPos;
                Vector3 p = head.position + (originalPosition - startPos);
                posCurves[0].AddKey(t, p.x);
                posCurves[1].AddKey(t, p.y);
                posCurves[2].AddKey(t, p.z);
            }
            WriteRotation(output, head.path, rotCurves);
            WriteChannels(output, head.path, PositionProperties, posCurves);
        }

        private static AnimationCurve[] ReadChannels(AnimationClip clip, string path, string[] properties)
        {
            AnimationCurve[] curves = new AnimationCurve[properties.Length];
            for (int i = 0; i < properties.Length; i++)
            {
                curves[i] = GetChannel(clip, path, properties[i]);
                if (curves[i] == null) return null;
            }
            return curves;
        }

        private static AnimationCurve GetChannel(AnimationClip clip, string path, string property)
        {
            return AnimationUtility.GetEditorCurve(clip,
                EditorCurveBinding.FloatCurve(path, typeof(Transform), property));
        }

        private static Quaternion ReadRotation(AnimationCurve[] curves, float t)
        {
            return new Quaternion(curves[0].Evaluate(t), curves[1].Evaluate(t),
                curves[2].Evaluate(t), curves[3].Evaluate(t)).normalized;
        }

        private static Vector3 ReadPosition(AnimationCurve[] curves, float t)
        {
            return new Vector3(curves[0].Evaluate(t), curves[1].Evaluate(t),
                curves[2].Evaluate(t));
        }

        private static Quaternion Continuous(Quaternion previous, Quaternion current)
        {
            return Quaternion.Dot(previous, current) < 0f
                ? new Quaternion(-current.x, -current.y, -current.z, -current.w)
                : current;
        }

        private static AnimationCurve[] NewCurves(int n)
        {
            var curves = new AnimationCurve[n];
            for (int i = 0; i < n; i++) curves[i] = new AnimationCurve();
            return curves;
        }

        private static void AddRotation(AnimationCurve[] curves, float t, Quaternion q)
        {
            curves[0].AddKey(t, q.x);
            curves[1].AddKey(t, q.y);
            curves[2].AddKey(t, q.z);
            curves[3].AddKey(t, q.w);
        }

        private static void WriteRotation(AnimationClip clip, string path, AnimationCurve[] curves)
        {
            WriteChannels(clip, path, RotationProperties, curves);
        }

        private static void WriteChannels(AnimationClip clip, string path,
            string[] properties, AnimationCurve[] curves)
        {
            for (int i = 0; i < properties.Length; i++)
            {
                AnimationUtility.SetEditorCurve(clip,
                    EditorCurveBinding.FloatCurve(path, typeof(Transform), properties[i]),
                    curves[i]);
            }
        }
    }
}
