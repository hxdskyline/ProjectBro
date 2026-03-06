using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "AvatarAnimationDefinition", menuName = "Game/Avatar/Animation Definition")]
public class AvatarAnimationDefinition : ScriptableObject
{
    [SerializeField] private string _avatarId;
    [SerializeField] private List<ActionDefinition> _actions = new List<ActionDefinition>();

    public string AvatarId => _avatarId;
    public IReadOnlyList<ActionDefinition> Actions => _actions;

    public ActionDefinition GetAction(AvatarActionType actionType)
    {
        for (int i = 0; i < _actions.Count; i++)
        {
            if (_actions[i].ActionType == actionType)
            {
                return _actions[i];
            }
        }

        return null;
    }

    [Serializable]
    public class ActionDefinition
    {
        [SerializeField] private AvatarActionType _actionType;
        [SerializeField] private List<string> _frameAddresses = new List<string>();
        [SerializeField] private float _fps = 12f;
        [SerializeField] private bool _loop = true;

        public AvatarActionType ActionType => _actionType;
        public IReadOnlyList<string> FrameAddresses => _frameAddresses;
        public float FPS => Mathf.Max(1f, _fps);
        public bool Loop => _loop;
    }
}
