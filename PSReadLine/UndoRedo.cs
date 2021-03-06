﻿using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace PSConsoleUtilities
{
    public partial class PSConsoleReadLine
    {
        private void SaveEditItem(EditItem editItem)
        {
            // If there is some sort of edit after an undo, forget
            // the
            int removeCount = _edits.Count - _undoEditIndex;
            if (removeCount > 0)
            {
                _edits.RemoveRange(_undoEditIndex, removeCount);
            }
            _edits.Add(editItem);
            _undoEditIndex = _edits.Count;
            _editGroupCount++;
        }

        private void StartEditGroup()
        {
            _pushedEditGroupCount.Push(_editGroupCount);
            _editGroupCount = 0;
        }

        private void EndEditGroup()
        {
            // The last _editGroupCount edits are treated as a single item for undo
            var start = _edits.Count - _editGroupCount;
            var groupedEditItems = _edits.GetRange(start, _editGroupCount);
            _edits.RemoveRange(start, _editGroupCount);
            SaveEditItem(GroupedEdit.Create(groupedEditItems));
            _editGroupCount = _pushedEditGroupCount.Pop();
        }

        /// <summary>
        /// Undo a previous edit.
        /// </summary>
        public static void Undo(ConsoleKeyInfo? key = null, object arg = null)
        {
            if (_singleton._undoEditIndex > 0)
            {
                _singleton._edits[_singleton._undoEditIndex - 1].Undo();
                _singleton._undoEditIndex--;
                _singleton.Render();
            }
            else
            {
                Ding();
            }
        }

        /// <summary>
        /// Undo an undo.
        /// </summary>
        public static void Redo(ConsoleKeyInfo? key = null, object arg = null)
        {
            if (_singleton._undoEditIndex < _singleton._edits.Count)
            {
                _singleton._edits[_singleton._undoEditIndex].Redo();
                _singleton._undoEditIndex++;
                _singleton.Render();
            }
            else
            {
                Ding();
            }
        }

        abstract class EditItem
        {
            public abstract void Undo();
            public abstract void Redo();
        }

        [DebuggerDisplay("Insert '{_insertedCharacter}' ({_insertStartPosition})")]
        class EditItemInsertChar : EditItem
        {
            // The character inserted is not needed for undo, only for redo
            private char _insertedCharacter;
            private int _insertStartPosition;

            public static EditItem Create(char character, int position)
            {
                return new EditItemInsertChar
                {
                    _insertedCharacter = character,
                    _insertStartPosition = position
                };
            }

            public override void Undo()
            {
                Debug.Assert(_singleton._buffer[_insertStartPosition] == _insertedCharacter, "Character to undo is not what it should be");
                _singleton._buffer.Remove(_insertStartPosition, 1);
                _singleton._current = _insertStartPosition;
            }

            public override void Redo()
            {
                _singleton._buffer.Insert(_insertStartPosition, _insertedCharacter);
                _singleton._current++;
            }
        }

        [DebuggerDisplay("Insert '{_insertedString}' ({_insertStartPosition})")]
        class EditItemInsertString : EditItem
        {
            // The string inserted tells us the length to delete on undo.
            // The contents of the string are only needed for redo.
            private string _insertedString;
            private int _insertStartPosition;

            public static EditItem Create(string str, int position)
            {
                return new EditItemInsertString
                {
                    _insertedString = str,
                    _insertStartPosition = position
                };
            }

            public override void Undo()
            {
                Debug.Assert(_singleton._buffer.ToString(_insertStartPosition, _insertedString.Length).Equals(_insertedString),
                    "Character to undo is not what it should be");
                _singleton._buffer.Remove(_insertStartPosition, _insertedString.Length);
                _singleton._current = _insertStartPosition;
            }

            public override void Redo()
            {
                _singleton._buffer.Insert(_insertStartPosition, _insertedString);
                _singleton._current += _insertedString.Length;
            }
        }

        [DebuggerDisplay("Delete '{_deletedString}' ({_deleteStartPosition})")]
        class EditItemDelete : EditItem
        {
            private string _deletedString;
            private int _deleteStartPosition;

            public static EditItem Create(string str, int position)
            {
                return new EditItemDelete
                {
                    _deletedString = str,
                    _deleteStartPosition = position
                };
            }

            public override void Undo()
            {
                _singleton._buffer.Insert(_deleteStartPosition, _deletedString);
                _singleton._current = _deleteStartPosition + _deletedString.Length;
            }

            public override void Redo()
            {
                _singleton._buffer.Remove(_deleteStartPosition, _deletedString.Length);
                _singleton._current = _deleteStartPosition;
            }
        }

        class GroupedEdit : EditItem
        {
            private List<EditItem> _groupedEditItems;

            public static EditItem Create(List<EditItem> groupedEditItems)
            {
                return new GroupedEdit {_groupedEditItems = groupedEditItems};
            }

            public override void Undo()
            {
                for (int i = _groupedEditItems.Count - 1; i >= 0; i--)
                {
                    _groupedEditItems[i].Undo();
                }
            }

            public override void Redo()
            {
                foreach (var editItem in _groupedEditItems)
                {
                    editItem.Redo();
                }
            }
        }
    }
}
