using AutoHook.Conditions;

namespace AutoHook.Ui;

public static class ConditionParamUi
{
    public static void DrawParams(Condition cond)
    {
        var def = ConditionRegistry.Registry.Get(cond.TypeId);
        if (def == null)
            return;

        def.DrawParams?.Invoke(cond);
    }
}

