using System.Collections.Generic;

namespace ILStructureParser
{
    internal class ILFMS
    {
        #region States

        public enum StateIdentifier
        {
            Start,
            DotToken,
            Field,
            Event,
            Method,
            Property,
            Class
        }

        public class State
        {
            public StateIdentifier StateId;
            public int StateValueIndex;
            public bool IsFinal;
        }
    
        private readonly IReadOnlyDictionary<char, StateIdentifier> _dotTokenTransitions = new Dictionary<char, StateIdentifier>()
        {
            { 'c', StateIdentifier.Class    },
            { 'm', StateIdentifier.Method   },
            { 'p', StateIdentifier.Property },
            { 'f', StateIdentifier.Field    },
            { 'e', StateIdentifier.Event    },
        };

        private readonly IReadOnlyDictionary<StateIdentifier, string> _tokens = new Dictionary<StateIdentifier, string>()
        {
            { StateIdentifier.Class,     "class"    },
            { StateIdentifier.Method,    "method"   },
            { StateIdentifier.Property,  "property" },
            { StateIdentifier.Field,     "field"    },
            { StateIdentifier.Event,     "event"    }
        };

        #endregion

        public ILFMS()
        {
            _state = new State() { StateId = StateIdentifier.Start };
        }

        private State _state;
        public State CurrentState
        {
            get { return _state; }
        }

        public void GoToNextState(char value)
        {
            if (_state.StateId == StateIdentifier.Start)
            {
                if (value == '.')
                {
                    _state.StateId = StateIdentifier.DotToken;
                }
            }
            else if (_state.StateId == StateIdentifier.DotToken)
            {
                _state.StateId = _dotTokenTransitions.ContainsKey(value) ? _dotTokenTransitions[value] : StateIdentifier.Start;
                _state.StateValueIndex = 0;
            }
            else if (_tokens[_state.StateId][_state.StateValueIndex + 1] == value)
            {
                _state.StateValueIndex++;
                _state.IsFinal = _state.StateValueIndex == _tokens[_state.StateId].Length - 1;
            }
            else
            {
                _state.StateId = StateIdentifier.Start;
            }
        }

        public void Restart()
        {
            _state.StateId = StateIdentifier.Start;
            _state.IsFinal = false;
        }
    }
}
