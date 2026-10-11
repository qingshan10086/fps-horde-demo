#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;

namespace DSH.WaveSystem.EditorTools
{
    /// <summary>
    /// WaveManager 的自定义 Inspector：
    ///   - 运行时实时显示当前波次 / 存活怪物数 / 待生成数 / 计时 / 池占用；
    ///   - 提供开始、下一波、强制完成、停止、重置、清池等调试按钮；
    ///   - 顺手给出配置体检（没生成点、没配置怪物等）。
    ///
    /// 不需要可以整个删除本文件，不影响运行时代码。
    /// </summary>
    [CustomEditor(typeof(WaveManager))]
    [CanEditMultipleObjects]
    public class WaveManagerEditor : Editor
    {
        private static readonly Color k_HeaderColor = new Color(1f, 0.62f, 0.2f);

        public override void OnInspectorGUI()
        {
            var manager = (WaveManager)target;

            DrawRuntimeStatus(manager);

            EditorGUILayout.Space(4f);
            DrawControlButtons(manager);
            EditorGUILayout.Space(4f);

            DrawDefaultInspector();

            if (GUI.changed) EditorUtility.SetDirty(manager);

            // 运行时每帧重绘，保证计数是「实时」的
            if (Application.isPlaying) Repaint();
        }

        // ─────────────────────────────────────────────────────────
        //  运行时状态面板
        // ─────────────────────────────────────────────────────────

        private void DrawRuntimeStatus(WaveManager manager)
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            var titleStyle = new GUIStyle(EditorStyles.boldLabel) { fontSize = 12 };
            EditorGUILayout.LabelField("波次调试信息 (Runtime Debug)", titleStyle);

            if (!Application.isPlaying)
            {
                EditorGUILayout.HelpBox("进入 Play 模式后这里会显示实时数据。", MessageType.Info);
                EditorGUILayout.EndVertical();
                return;
            }

            EditorGUILayout.LabelField("当前波次",
                $"{Mathf.Max(0, manager.CurrentWaveIndex + 1)} / {manager.TotalWaves}" +
                (manager.CurrentWave != null ? $"   «{manager.CurrentWave.waveName}»" : ""));

            string state;
            if (manager.IsWaveInProgress) state = "战斗中";
            else if (manager.StartDelayRemaining > 0f) state = $"倒计时 {manager.StartDelayRemaining:F1}s";
            else if (manager.CurrentWaveIndex >= 0) state = "波间 / 已结束";
            else state = "未开始";

            EditorGUILayout.LabelField("状态", state);
            EditorGUILayout.LabelField("存活怪物数", manager.AliveEnemyCount.ToString());
            EditorGUILayout.LabelField("场上实例数（含尸体）", manager.LiveEnemyCount.ToString());
            EditorGUILayout.LabelField("待生成数量", manager.PendingSpawnCount.ToString());

            // 生成进度条
            Rect rect = EditorGUILayout.GetControlRect(false, 18f);
            float progress = manager.WaveTotalCount > 0
                ? (float)manager.WaveSpawnedCount / manager.WaveTotalCount
                : 0f;
            EditorGUI.ProgressBar(rect, progress,
                $"生成进度 {manager.WaveSpawnedCount}/{manager.WaveTotalCount}");

            EditorGUILayout.LabelField("本波已用时", $"{manager.WaveElapsed:F1} s");

            float remaining = manager.WaveTimeRemaining;
            EditorGUILayout.LabelField("本波剩余时间", remaining < 0f ? "无时间限制" : $"{remaining:F1} s");

            EditorGUILayout.LabelField("池中空闲实例", manager.PooledIdleCount.ToString());

            EditorGUILayout.EndVertical();
        }

        // ─────────────────────────────────────────────────────────
        //  控制按钮
        // ─────────────────────────────────────────────────────────

        private void DrawControlButtons(WaveManager manager)
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("流程控制", EditorStyles.boldLabel);

            using (new EditorGUI.DisabledScope(!Application.isPlaying))
            {
                EditorGUILayout.BeginHorizontal();

                if (GUILayout.Button("开始波次")) manager.StartWaves();
                if (GUILayout.Button("立即下一波")) manager.StartNextWave();

                EditorGUILayout.EndHorizontal();

                EditorGUILayout.BeginHorizontal();

                using (new EditorGUI.DisabledScope(!manager.IsWaveInProgress))
                {
                    if (GUILayout.Button("强制完成本波")) manager.ForceCompleteWave();
                }

                using (new EditorGUI.DisabledScope(!manager.IsWaveInProgress))
                {
                    if (GUILayout.Button("满足自定义条件")) manager.CompleteCustomCondition();
                }

                EditorGUILayout.EndHorizontal();

                EditorGUILayout.BeginHorizontal();

                if (GUILayout.Button("停止波次")) manager.StopWaves();
                if (GUILayout.Button("重置")) manager.ResetWaves(false);
                if (GUILayout.Button("清空对象池")) manager.ClearPool(true);

                EditorGUILayout.EndHorizontal();
            }

            if (!Application.isPlaying)
            {
                EditorGUILayout.HelpBox("调试按钮仅在 Play 模式下可用。", MessageType.None);
            }

            EditorGUILayout.EndVertical();

            DrawConfigurationWarnings(manager);
        }

        private void DrawConfigurationWarnings(WaveManager manager)
        {
            var problems = new List<string>();

            if (manager.spawnPoints == null || manager.spawnPoints.Count == 0)
                problems.Add("没有配置任何生成点（spawnPoints 为空）—— 将退化为在 WaveManager 自身位置生成。");

            // 寻路怪物 + 对象池是经典冲突点，这里提前提醒。
            bool anyNavMeshAgent = false;
            if (!manager.disableNavAgentOnDeath) anyNavMeshAgent = HasNavMeshAgentPrefab(manager);
            if (anyNavMeshAgent)
                problems.Add("怪物预制体带 NavMeshAgent，但 disableNavAgentOnDeath 被关掉了。" +
                             "死亡延迟期内 AI 脚本若仍在调用 SetDestination()/isStopped，会刷出 " +
                             "『\"SetDestination\"/\"Resume\" can only be called on an active agent...』，建议重新勾选。");

            if (manager.waves == null || manager.waves.Count == 0)
                problems.Add("没有配置任何波次（waves 为空）。");
            else
            {
                for (int i = 0; i < manager.waves.Count; i++)
                {
                    var wave = manager.waves[i];
                    if (wave == null)
                    {
                        problems.Add($"第 {i + 1} 波配置为空引用。");
                        continue;
                    }

                    if (!wave.HasEnemies)
                        problems.Add($"第 {i + 1} 波（{wave.waveName}）没有任何有效怪物配置。");

                    if ((wave.conditions & WaveConditionFlags.TimeLimit) != 0 && wave.timeLimit <= 0f)
                        problems.Add($"第 {i + 1} 波勾选了 TimeLimit 但 timeLimit <= 0，会立即超时。");

                    if (wave.spawnMode == SpawnMode.RandomInterval &&
                        wave.minSpawnInterval > wave.maxSpawnInterval)
                        problems.Add($"第 {i + 1} 波 minSpawnInterval > maxSpawnInterval，运行时会被自动纠正。");
                }
            }

            if (problems.Count == 0) return;

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("配置体检", EditorStyles.boldLabel);
            for (int i = 0; i < problems.Count; i++)
            {
                EditorGUILayout.HelpBox(problems[i], MessageType.Warning);
            }
            EditorGUILayout.EndVertical();
        }

        /// <summary>检查配置里出现过的怪物预制体是否带 NavMeshAgent。</summary>
        private static bool HasNavMeshAgentPrefab(WaveManager manager)
        {
            if (manager.waves == null) return false;

            for (int i = 0; i < manager.waves.Count; i++)
            {
                var wave = manager.waves[i];
                if (wave == null) continue;

                for (int e = 0; e < wave.enemies.Count; e++)
                {
                    var prefab = wave.enemies[e] != null ? wave.enemies[e].prefab : null;
                    if (prefab == null) continue;

                    if (prefab.GetComponentInChildren<NavMeshAgent>(true) != null) return true;
                }
            }

            return false;
        }
    }
}
#endif
