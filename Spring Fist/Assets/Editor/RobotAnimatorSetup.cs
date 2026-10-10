using UnityEngine;
using UnityEditor;
using UnityEditor.Animations;

/// <summary>
/// 一次性配置工具：给 Assets/Animator/Robot1.controller 自动连线。
/// 脚本编译完成后会自动执行一次（幂等，已连好的不会重复连），
/// 也可通过菜单 Tools/机器人/配置Robot1动画器连线 手动执行。
///
/// 连线内容（与 RobotStateManager 代码的参数约定一致）：
///   Idle ──[Move=true]──▶ Move        （无Exit Time，过渡0.15s）
///   Move ──[Move=false]─▶ Idle        （无Exit Time，过渡0.15s）
///   AnyState ──[Attack=true]──▶ TwiceAttack （无Exit Time，过渡0.05s，不允许自切）
///   TwiceAttack ──[Exit Time=1，无条件]──▶ Idle （过渡0.1；不带Attack=false条件，否则非循环片段会卡死）
///   AnyState ──[Die Trigger]──▶ Death （无Exit Time，过渡0.1s，不允许自切）
/// </summary>
public static class RobotAnimatorSetup
{
    private const string ControllerPath = "Assets/Animator/Robot1.controller";

    [MenuItem("Tools/机器人/配置Robot1动画器连线")]
    private static void SetupFromMenu()
    {
        Setup(true);
    }

    [InitializeOnLoadMethod]
    private static void AutoSetup()
    {
        // 等编辑器就绪后执行；幂等，已全部连好时静默跳过
        EditorApplication.delayCall += () => Setup(false);
    }

    private static void Setup(bool verbose)
    {
        AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
        if (controller == null)
        {
            if (verbose)
            {
                Debug.LogWarning("[RobotAnimatorSetup] 未找到 " + ControllerPath);
            }
            return;
        }

        AnimatorStateMachine sm = controller.layers[0].stateMachine;
        AnimatorState idle = FindState(sm, "Idle");
        AnimatorState move = FindState(sm, "Move");
        AnimatorState twiceAttack = FindState(sm, "TwiceAttack");
        AnimatorState death = FindState(sm, "Death");
        if (idle == null || move == null || twiceAttack == null || death == null)
        {
            Debug.LogError("[RobotAnimatorSetup] 状态缺失（需要 Idle/Move/TwiceAttack/Death 四个状态），未做任何修改。");
            return;
        }

        bool changed = false;

        // 参数：Move(Bool) / Attack(Bool) / Die(Trigger)
        EnsureParameter(controller, "Move", AnimatorControllerParameterType.Bool, ref changed);
        EnsureParameter(controller, "Attack", AnimatorControllerParameterType.Bool, ref changed);
        EnsureParameter(controller, "Die", AnimatorControllerParameterType.Trigger, ref changed);

        // Idle → Move：Move=true
        AnimatorStateTransition t1 = EnsureStateTransition(idle, move, ref changed);
        t1.hasExitTime = false;
        t1.hasFixedDuration = true;
        t1.duration = 0.15f;
        t1.conditions = new[] { Condition(AnimatorConditionMode.If, "Move") };

        // Move → Idle：Move=false
        AnimatorStateTransition t2 = EnsureStateTransition(move, idle, ref changed);
        t2.hasExitTime = false;
        t2.hasFixedDuration = true;
        t2.duration = 0.15f;
        t2.conditions = new[] { Condition(AnimatorConditionMode.IfNot, "Move") };

        // AnyState → TwiceAttack：Attack=true，不允许自切
        AnimatorStateTransition t3 = EnsureAnyStateTransition(sm, twiceAttack, ref changed);
        t3.hasExitTime = false;
        t3.hasFixedDuration = true;
        t3.duration = 0.05f;
        t3.canTransitionToSelf = false;
        t3.conditions = new[] { Condition(AnimatorConditionMode.If, "Attack") };

        // TwiceAttack → Idle：动画播完（Exit Time=1）无条件切回Idle。
        // 不带Attack=false条件：非循环片段在Exit Time到达的瞬间条件不满足就永远不会再触发
        // （Attack布尔是动画播完后下一帧才由代码收回的，那一刻Exit Time已经错过），会卡死在TwiceAttack。
        // 是否需要再次攻击由代码的Attack布尔控制（Any State→TwiceAttack的条件），出口不需要条件。
        AnimatorStateTransition t4 = EnsureStateTransition(twiceAttack, idle, ref changed);
        t4.hasExitTime = true;
        t4.exitTime = 1f;
        t4.hasFixedDuration = false; // exitTime按归一化时间（1=片段播完）
        t4.duration = 0.1f;
        t4.conditions = new AnimatorCondition[0];

        // AnyState → Death：Die Trigger，不允许自切
        AnimatorStateTransition t5 = EnsureAnyStateTransition(sm, death, ref changed);
        t5.hasExitTime = false;
        t5.hasFixedDuration = true;
        t5.duration = 0.1f;
        t5.canTransitionToSelf = false;
        t5.conditions = new[] { Condition(AnimatorConditionMode.If, "Die") };

        // 每次都应用设置并保存：连线已存在时只校正属性（如去掉过时的条件），内容不变则写入无副作用。
        // 修改前备份一份到工程根目录（Assets之外，不参与导入）
        try
        {
            string backupPath = "Robot1.controller.before-auto-setup.backup";
            if (!System.IO.File.Exists(backupPath))
            {
                System.IO.File.Copy(ControllerPath, backupPath);
            }
        }
        catch (System.Exception e)
        {
            Debug.LogWarning("[RobotAnimatorSetup] 备份失败（不影响连线）：" + e.Message);
        }

        EditorUtility.SetDirty(controller);
        AssetDatabase.SaveAssets();
        Debug.Log("[RobotAnimatorSetup] Robot1.controller 连线已校验/修复：Idle↔Move、AnyState→TwiceAttack、TwiceAttack→Idle(无条件)、AnyState→Death。" +
                  "如需回滚，用工程根目录的 Robot1.controller.before-auto-setup.backup 覆盖 Assets/Animator/Robot1.controller。");
    }

    private static AnimatorState FindState(AnimatorStateMachine sm, string name)
    {
        foreach (ChildAnimatorState child in sm.states)
        {
            if (child.state != null && child.state.name == name)
            {
                return child.state;
            }
        }
        return null;
    }

    private static void EnsureParameter(AnimatorController controller, string name, AnimatorControllerParameterType type, ref bool changed)
    {
        foreach (AnimatorControllerParameter p in controller.parameters)
        {
            if (p.name == name)
            {
                if (p.type != type)
                {
                    Debug.LogError($"[RobotAnimatorSetup] 参数 {name} 已存在但类型不对（需要 {type}），请在Animator的Parameters面板手动修正后重试。");
                }
                return;
            }
        }
        controller.AddParameter(name, type);
        changed = true;
    }

    private static AnimatorStateTransition EnsureStateTransition(AnimatorState from, AnimatorState to, ref bool changed)
    {
        foreach (AnimatorStateTransition t in from.transitions)
        {
            if (t.destinationState == to)
            {
                return t;
            }
        }
        changed = true;
        return from.AddTransition(to);
    }

    private static AnimatorStateTransition EnsureAnyStateTransition(AnimatorStateMachine sm, AnimatorState to, ref bool changed)
    {
        foreach (AnimatorStateTransition t in sm.anyStateTransitions)
        {
            if (t.destinationState == to)
            {
                return t;
            }
        }
        changed = true;
        return sm.AddAnyStateTransition(to);
    }

    private static AnimatorCondition Condition(AnimatorConditionMode mode, string parameter)
    {
        return new AnimatorCondition { mode = mode, parameter = parameter, threshold = 0f };
    }
}
