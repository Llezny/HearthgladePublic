using System;
using System.Collections.Generic;

namespace Hearthglade.Gameplay.Environment
{
    /// <summary>One entry of the dropdown that opens when the player holds a finger on an object.</summary>
    public readonly struct ContextAction
    {
        public readonly string Label;
        public readonly Action Execute;

        public ContextAction( string label, Action execute )
        {
            Label = label;
            Execute = execute;
        }
    }

    /// <summary>
    /// An object that has a dropdown of actions. A tap on it still does its normal interaction (<see cref="IInteractable"/>); holding a finger on it
    /// opens the dropdown instead of the tooltip (see TooltipManager).
    /// </summary>
    public interface IContextActions
    {
        IReadOnlyList<ContextAction> ContextActions { get; }
    }
}
