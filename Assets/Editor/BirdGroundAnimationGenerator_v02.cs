// BirdGroundAnimationGenerator v0.2.1
// Coloque SOMENTE ESTE ARQUIVO em Assets/Editor e REMOVA a versao v0.1.
// Gera AnimationClips para o rig Generic original de Bird.fbx.
// Nao modifica FBX, Animator Controller nem scripts de gameplay.
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Scripts.Gameplay.Bird.Editor
{
    public sealed class BirdGroundAnimationGenerator : EditorWindow
    {
        private const string DefaultFolder = "Assets/_Game/Animations/BirdGround";
        private GameObject modelObject;
        private string outputFolder = DefaultFolder;

        private float walkDuration = 0.80f;
        private float runDuration = 0.45f;
        private float walkStride = 24f;
        private float runStride = 39f;
        private float walkKneeBend = 28f;
        private float runKneeBend = 43f;
        private float walkFootLift = 0.10f;
        private float runFootLift = 0.17f;
        private float wingFold = 68f;
        private float wingElbowFold = 24f;
        private float wingSwing = 4f;
        private bool reverseForward = false;
        private bool reverseKnee = false;

        private sealed class Bone
        {
            public Transform Transform;
            public string Path;
            public Quaternion Rotation;
            public Vector3 Position;
            public Vector3 AxisInParent;
            public float WorldThighLength;
        }

        private sealed class PoseRig
        {
            public Animator Animator;
            public Dictionary<string, Bone> Map;
            public List<Bone> Bones;
            public Vector3 Forward;
            public Vector3 Up;
        }

        private readonly struct Gait
        {
            public readonly string Name;
            public readonly float Duration;
            public readonly float Stride;
            public readonly float Knee;
            public readonly float Lift;
            public readonly bool IsRun;

            public Gait(string name, float duration, float stride, float knee, float lift, bool isRun)
            {
                Name = name;
                Duration = duration;
                Stride = stride;
                Knee = knee;
                Lift = lift;
                IsRun = isRun;
            }
        }

        private static readonly string[] RequiredBones =
        {
            "root", "pelvis", "body1", "head",
            "leg_L_1", "leg_L_2", "foot_L",
            "leg_R_1", "leg_R_2", "foot_R",
            "wing_L_1_1", "wing_R_1_1",
            "wing_L_2_1", "wing_R_2_1"
        };

        [MenuItem("Tools/Bird Game/Ground Animation Generator v0.2")]
        private static void Open()
        {
            var window = GetWindow<BirdGroundAnimationGenerator>("Bird Ground Anim v0.2");
            window.minSize = new Vector2(440, 530);
            if (Selection.activeGameObject != null)
                window.modelObject = Selection.activeGameObject;
        }

        private void OnGUI()
        {
            EditorGUILayout.LabelField("Bird Ground Animations — v0.2.1", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Corrige o movimento simultaneo das pernas e as asas abertas em T. " +
                "Use uma COPIA LIMPA do Bird.fbx na cena, fora do Play Mode, " +
                "com pose de repouso original (sem Preview de animação).",
                MessageType.Info);
            modelObject = (GameObject)EditorGUILayout.ObjectField(
                "Modelo na cena", modelObject, typeof(GameObject), true);
            outputFolder = EditorGUILayout.TextField("Destino", outputFolder);

            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField("Passadas alternadas", EditorStyles.boldLabel);
            walkDuration = EditorGUILayout.Slider("Ciclo Walk (seg)", walkDuration, 0.5f, 1.5f);
            runDuration = EditorGUILayout.Slider("Ciclo Run (seg)", runDuration, 0.3f, 0.85f);
            walkStride = EditorGUILayout.Slider("Passada Walk", walkStride, 8f, 55f);
            runStride = EditorGUILayout.Slider("Passada Run", runStride, 12f, 70f);
            walkKneeBend = EditorGUILayout.Slider("Joelho Walk", walkKneeBend, 0f, 70f);
            runKneeBend = EditorGUILayout.Slider("Joelho Run", runKneeBend, 0f, 85f);
            walkFootLift = EditorGUILayout.Slider("Levantar pe Walk", walkFootLift, 0f, 0.3f);
            runFootLift = EditorGUILayout.Slider("Levantar pe Run", runFootLift, 0f, 0.35f);

            EditorGUILayout.Space(7);
            EditorGUILayout.LabelField("Asas recolhidas", EditorStyles.boldLabel);
            wingFold = EditorGUILayout.Slider("Fechar asas", wingFold, 0f, 105f);
            wingElbowFold = EditorGUILayout.Slider("Dobrar ponta", wingElbowFold, 0f, 95f);
            wingSwing = EditorGUILayout.Slider("Balanco das asas", wingSwing, 0f, 12f);

            EditorGUILayout.Space(7);
            EditorGUILayout.LabelField("Calibracao opcional", EditorStyles.boldLabel);
            reverseForward = EditorGUILayout.Toggle("Inverter direcao da passada", reverseForward);
            reverseKnee = EditorGUILayout.Toggle("Inverter dobra do joelho", reverseKnee);

            EditorGUILayout.Space(12);
            GUI.enabled = !EditorApplication.isPlaying && modelObject != null;
            if (GUILayout.Button("GERAR GroundIdle, Walk e Run (v0.2)", GUILayout.Height(38)))
                Generate();
            GUI.enabled = true;
            EditorGUILayout.HelpBox(
                "A geracao nao altera a cena. Os novos clips recebem sufixo v02; " +
                "as versoes anteriores nao sao sobrescritas.", MessageType.None);
        }

        private void Generate()
        {
            if (!TryCaptureRig(out PoseRig rig, out string error))
            {
                EditorUtility.DisplayDialog("Bird Animation Generator", error, "OK");
                return;
            }

            if (string.IsNullOrWhiteSpace(outputFolder) ||
                !outputFolder.StartsWith("Assets/", StringComparison.Ordinal))
            {
                EditorUtility.DisplayDialog("Bird Animation Generator",
                    "Pasta de destino invalida. Use caminho iniciado em Assets/.", "OK");
                return;
            }

            EnsureFolders(outputFolder);

            var idle = new Gait("Bird_GroundIdle_v02", 1.20f, 0f, 0f, 0f, false);
            var walk = new Gait("Bird_Walk_v02", walkDuration, walkStride, walkKneeBend,
                walkFootLift, false);
            var run = new Gait("Bird_Run_v02", runDuration, runStride, runKneeBend,
                runFootLift, true);

            string idlePath = WriteClip(rig, idle);
            string walkPath = WriteClip(rig, walk);
            string runPath = WriteClip(rig, run);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Selection.activeObject = AssetDatabase.LoadAssetAtPath<AnimationClip>(walkPath);
            Debug.Log("[BIRD ANIM v0.2] Clips criados:\n" +
                idlePath + "\n" + walkPath + "\n" + runPath);
            EditorUtility.DisplayDialog("Bird Animation Generator",
                "Clips criados!\n\n" + idlePath + "\n" + walkPath + "\n" +
                runPath + "\n\nTeste os clips em um Animator temporario.", "OK");
        }

        private bool TryCaptureRig(out PoseRig rig, out string error)
        {
            rig = null;
            error = "";
            if (modelObject == null)
            {
                error = "Selecione uma instancia do modelo.";
                return false;
            }

            Animator animator = modelObject.GetComponent<Animator>();
            if (animator == null)
                animator = modelObject.GetComponentInChildren<Animator>(true);
            if (animator == null)
            {
                error = "Nao foi encontrado um Animator no modelo selecionado.";
                return false;
            }
            if (animator.isHuman)
            {
                error = "Rig Humanoid detectado. Configure o Bird.fbx como Generic.";
                return false;
            }

            var map = new Dictionary<string, Bone>(StringComparer.Ordinal);
            foreach (Transform t in animator.GetComponentsInChildren<Transform>(true))
            {
                if (map.ContainsKey(t.name))
                    continue;
                map.Add(t.name, new Bone
                {
                    Transform = t,
                    Path = AnimationUtility.CalculateTransformPath(t, animator.transform),
                    Rotation = t.localRotation,
                    Position = t.localPosition,
                    AxisInParent = Vector3.right
                });
            }

            foreach (string name in RequiredBones)
            {
                if (!map.ContainsKey(name))
                {
                    error = "Faltando osso: " + name + ". Selecione a instancia do Bird.fbx original.";
                    return false;
                }
            }

            Transform rootBone = map["root"].Transform;
            var allBones = new List<Bone>();
            foreach (Transform t in rootBone.GetComponentsInChildren<Transform>(true))
                if (map.TryGetValue(t.name, out Bone pose) && pose.Transform == t)
                    allBones.Add(pose);

            Vector3 worldUp = animator.transform.up.normalized;
            Vector3 worldForward = animator.transform.forward.normalized *
                (reverseForward ? -1f : 1f);

            // Eixo calculado de acordo com o comprimento REAL de cada perna.
            // Assim as duas pernas se movimentam em direcoes OPOSTAS no espaco
            // do personagem, mesmo que a orientacao local dos ossos seja espelhada.
            SetupLeg(map, "leg_L_1", "leg_L_2", worldForward, worldUp);
            SetupLeg(map, "leg_R_1", "leg_R_2", worldForward, worldUp);
            SetupLeg(map, "leg_L_2", "foot_L", worldForward, worldUp);
            SetupLeg(map, "leg_R_2", "foot_R", worldForward, worldUp);
            CopyAxis(map, "leg_L_2", "foot_L");
            CopyAxis(map, "leg_R_2", "foot_R");

            // O primeiro segmento da asa aponta para fora. A rotacao
            // e calculada para aproximar esse segmento do eixo "baixo".
            SetupAim(map, "wing_L_1_1", "wing_L_2_1", -worldUp);
            SetupAim(map, "wing_R_1_1", "wing_R_2_1", -worldUp);
            SetupAim(map, "wing_L_2_1", "wing_L_3_1", -worldForward);
            SetupAim(map, "wing_R_2_1", "wing_R_3_1", -worldForward);

            rig = new PoseRig
            {
                Animator = animator,
                Bones = allBones,
                Map = map,
                Forward = worldForward,
                Up = worldUp
            };
            return true;
        }

        private static void SetupLeg(Dictionary<string, Bone> map,
            string parentName, string childName, Vector3 forward, Vector3 up)
        {
            Bone bone = map[parentName];
            Transform child = map[childName].Transform;
            Vector3 segment = child.position - bone.Transform.position;
            bone.WorldThighLength = segment.magnitude;
            Vector3 axisWorld = Vector3.Cross(segment.normalized, forward);
            if (axisWorld.sqrMagnitude < 0.001f)
                axisWorld = Vector3.Cross(up, forward);
            bone.AxisInParent = ToParentAxis(bone.Transform, axisWorld);
        }

        private static void CopyAxis(Dictionary<string, Bone> map,
            string source, string target)
        {
            Bone from = map[source];
            Bone to = map[target];
            Vector3 axisWorld = from.Transform.parent.TransformDirection(from.AxisInParent);
            to.AxisInParent = ToParentAxis(to.Transform, axisWorld);
            to.WorldThighLength = from.WorldThighLength;
        }

        private static void SetupAim(Dictionary<string, Bone> map,
            string source, string next, Vector3 desiredWorldDirection)
        {
            if (!map.TryGetValue(next, out Bone end))
                return;
            Bone bone = map[source];
            Vector3 direction = (end.Transform.position - bone.Transform.position).normalized;
            Vector3 axisWorld = Vector3.Cross(direction, desiredWorldDirection);
            if (axisWorld.sqrMagnitude < 0.001f)
                axisWorld = bone.Transform.forward;
            bone.AxisInParent = ToParentAxis(bone.Transform, axisWorld);
        }

        private static Vector3 ToParentAxis(Transform bone, Vector3 worldAxis)
        {
            return (bone.parent != null
                ? bone.parent.InverseTransformDirection(worldAxis.normalized)
                : worldAxis.normalized).normalized;
        }

        private string WriteClip(PoseRig rig, Gait gait)
        {
            var clip = new AnimationClip { name = gait.Name, frameRate = 60f };
            const int Samples = 40;

            foreach (Bone bone in rig.Bones)
            {
                string name = bone.Transform.name;
                bool moving = IsMoving(name);
                int steps = moving ? Samples : 1;
                var rotKeys = CreateChannels(4);
                var posKeys = CreateChannels(3);
                Quaternion previous = bone.Rotation;

                for (int i = 0; i <= steps; i++)
                {
                    float time = gait.Duration * i / steps;
                    float phase = Mathf.PI * 2f * i / steps;
                    Quaternion rotation = PoseRotation(bone, gait, phase);
                    Vector3 position = PosePosition(rig, bone, gait, phase);
                    if (Quaternion.Dot(previous, rotation) < 0f)
                        rotation = new Quaternion(-rotation.x, -rotation.y,
                            -rotation.z, -rotation.w);
                    previous = rotation;
                    rotKeys[0].Add(new Keyframe(time, rotation.x));
                    rotKeys[1].Add(new Keyframe(time, rotation.y));
                    rotKeys[2].Add(new Keyframe(time, rotation.z));
                    rotKeys[3].Add(new Keyframe(time, rotation.w));
                    posKeys[0].Add(new Keyframe(time, position.x));
                    posKeys[1].Add(new Keyframe(time, position.y));
                    posKeys[2].Add(new Keyframe(time, position.z));
                }

                WriteChannel(clip, bone.Path, "m_LocalRotation.x", rotKeys[0]);
                WriteChannel(clip, bone.Path, "m_LocalRotation.y", rotKeys[1]);
                WriteChannel(clip, bone.Path, "m_LocalRotation.z", rotKeys[2]);
                WriteChannel(clip, bone.Path, "m_LocalRotation.w", rotKeys[3]);
                // Captura a pose estatica, evitando retornos a T-pose.
                WriteChannel(clip, bone.Path, "m_LocalPosition.x", posKeys[0]);
                WriteChannel(clip, bone.Path, "m_LocalPosition.y", posKeys[1]);
                WriteChannel(clip, bone.Path, "m_LocalPosition.z", posKeys[2]);
            }

            clip.EnsureQuaternionContinuity();
            SerializedObject serialized = new SerializedObject(clip);
            SerializedProperty loop = serialized.FindProperty("m_AnimationClipSettings.m_LoopTime");
            if (loop != null)
            {
                loop.boolValue = true;
                serialized.ApplyModifiedPropertiesWithoutUndo();
            }
            string path = AssetDatabase.GenerateUniqueAssetPath(
                outputFolder.TrimEnd('/') + "/" + gait.Name + ".anim");
            AssetDatabase.CreateAsset(clip, path);
            return path;
        }

        private Quaternion PoseRotation(Bone bone, Gait gait, float phase)
        {
            string name = bone.Transform.name;
            float left = phase;
            float right = phase + Mathf.PI;
            float leftSwing = Mathf.Cos(left) * gait.Stride;
            float rightSwing = Mathf.Cos(right) * gait.Stride;
            float leftLift = Mathf.Pow(Mathf.Max(0f, Mathf.Sin(left)), 1.15f);
            float rightLift = Mathf.Pow(Mathf.Max(0f, Mathf.Sin(right)), 1.15f);
            float kneeSign = reverseKnee ? -1f : 1f;
            bool idle = gait.Stride < 0.001f;
            Quaternion rest = bone.Rotation;

            switch (name)
            {
                case "leg_L_1": return Offset(bone, rest, leftSwing);
                case "leg_R_1": return Offset(bone, rest, rightSwing);
                case "leg_L_2": return Offset(bone, rest, kneeSign * gait.Knee * leftLift);
                case "leg_R_2": return Offset(bone, rest, kneeSign * gait.Knee * rightLift);
                case "foot_L": return Offset(bone, rest,
                    -leftSwing * 0.50f - kneeSign * gait.Knee * leftLift * 0.42f);
                case "foot_R": return Offset(bone, rest,
                    -rightSwing * 0.50f - kneeSign * gait.Knee * rightLift * 0.42f);
                case "wing_L_1_1": return Offset(bone, rest,
                    wingFold + (idle ? 0f : wingSwing * Mathf.Sin(left)));
                case "wing_R_1_1": return Offset(bone, rest,
                    wingFold + (idle ? 0f : wingSwing * Mathf.Sin(right)));
                case "wing_L_2_1": return Offset(bone, rest, wingElbowFold);
                case "wing_R_2_1": return Offset(bone, rest, wingElbowFold);
                case "pelvis": return rest * Quaternion.Euler(
                    gait.IsRun ? 7f : (idle ? 0f : 3f),
                    idle ? 0f : Mathf.Sin(phase) * 1.2f,
                    idle ? 0f : Mathf.Sin(phase) * 1.8f);
                case "body1": return rest * Quaternion.Euler(
                    idle ? Mathf.Sin(phase) * 1.2f : Mathf.Cos(phase * 2f) * 2.7f,
                    0f, idle ? 0f : Mathf.Sin(phase) * 1.5f);
                case "head": return rest * Quaternion.Euler(
                    idle ? Mathf.Sin(phase) * 1f : -Mathf.Cos(phase * 2f) * 3f,
                    idle ? 0f : Mathf.Sin(phase) * 1.3f, 0f);
                default: return rest;
            }
        }

        private static Quaternion Offset(Bone bone, Quaternion rest, float degrees)
        {
            // Rotacao no espaco do PAI, independente do espelhamento dos ossos.
            return Quaternion.AngleAxis(degrees, bone.AxisInParent) * rest;
        }

        private static Vector3 PosePosition(PoseRig rig, Bone bone, Gait gait, float phase)
        {
            Vector3 position = bone.Position;
            if (bone.Transform.name == "pelvis")
            {
                // Balanço sutil; o Rigidbody/Transform raiz nunca sao animados.
                float bob = gait.Stride <= 0f ? 0.006f * (0.5f + 0.5f * Mathf.Sin(phase))
                    : (gait.IsRun ? 0.015f : 0.007f) * (0.5f - 0.5f * Mathf.Cos(phase * 2f));
                position += bone.Transform.parent.InverseTransformVector(rig.Up * bob);
            }
            else if (bone.Transform.name == "foot_L" || bone.Transform.name == "foot_R")
            {
                // Translação relativa ao comprimento da perna, para nao depender
                // da escala do FBX (muitas vezes centímetro -> metro).
                bool isLeft = bone.Transform.name == "foot_L";
                float footPhase = phase + (isLeft ? 0f : Mathf.PI);
                float lift = Mathf.Pow(Mathf.Max(0f, Mathf.Sin(footPhase)), 1.2f);
                if (gait.Lift > 0f)
                {
                    Vector3 upDisplacement = rig.Up * (bone.WorldThighLength * gait.Lift * lift);
                    position += bone.Transform.parent.InverseTransformVector(upDisplacement);
                }
            }
            return position;
        }

        private static bool IsMoving(string name)
        {
            switch (name)
            {
                case "pelvis": case "body1": case "head":
                case "leg_L_1": case "leg_L_2": case "foot_L":
                case "leg_R_1": case "leg_R_2": case "foot_R":
                case "wing_L_1_1": case "wing_R_1_1":
                    return true;
                default: return false;
            }
        }

        private static List<Keyframe>[] CreateChannels(int n)
        {
            var channels = new List<Keyframe>[n];
            for (int i = 0; i < n; i++) channels[i] = new List<Keyframe>();
            return channels;
        }

        private static void WriteChannel(AnimationClip clip, string path,
            string property, List<Keyframe> keys)
        {
            var curve = new AnimationCurve(keys.ToArray());
            for (int i = 0; i < curve.length; i++) curve.SmoothTangents(i, 0f);
            AnimationUtility.SetEditorCurve(clip,
                EditorCurveBinding.FloatCurve(path, typeof(Transform), property), curve);
        }

        private static void EnsureFolders(string path)
        {
            string[] parts = path.Split('/');
            string current = parts[0];
            for (int i = 1; i < parts.Length; i++)
            {
                if (string.IsNullOrWhiteSpace(parts[i])) continue;
                string next = current + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next))
                    AssetDatabase.CreateFolder(current, parts[i]);
                current = next;
            }
        }
    }
}
