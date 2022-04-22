using System;
using System.Collections.Generic;
using System.Threading;
using System.Linq;
using System.Text;
using UnityEngine;

namespace DevConsole
{
    internal class BF
    {
        public bool Running => running;
        private bool running;

        private Queue<char> input = new Queue<char>();
        private object inputLock = new object();
        private readonly EventWaitHandle waitForInput;
        private Thread thread;

        public BF(string source, string[] flags)
        {
            running = true;

            waitForInput = new EventWaitHandle(false, EventResetMode.ManualReset);

            thread = new Thread(() => {
                Thread.Sleep(0);
                try
                {
                    if (flags.Contains("u64")) Run(source, flags, new LongTape());
                    else if (flags.Contains("u32")) Run(source, flags, new IntTape());
                    else if (flags.Contains("u16")) Run(source, flags, new ShortTape());
                    else Run(source, flags, new ByteTape());
                }
                catch (Exception e)
                {
                    GameConsole.WriteLineThreaded("BF program failed!", Color.red);
                    GameConsole.WriteLineThreaded(e.ToString(), Color.red);
                }
                running = false;
            });
            thread.Start();

            QuitWatcher.OnQuit += Abort;
        }

        public void Abort()
        {
            if (thread != null)
                thread.Abort();
            thread = null;
        }

        public void Input(string input)
        {
            lock (inputLock)
            {
                foreach (var c in input)
                {
                    if (c != '\r')
                        this.input.Enqueue(c);
                }

                // Notify any threads that there is input to consume
                if (this.input.Count > 0)
                    waitForInput.Set();
            }
        }

        private void Run<T>(string source, string[] flags, Tape<T> tape)
        {
            Instruction[] prgm;
            var output = new StringBuilder();
            Queue<char> localInput = null;
            int prgmLen;
            int prgmPtr = 0;
            int cellPtr = 0;
            bool dontYield = flags.Contains("fast");

            tape.AllowNeg = !flags.Contains("no_neg_cells");

            // Parse instructions
            {
                char[] chars = source.ToCharArray();
                int pos = 0;
                List<Instruction> prgmTemp = new List<Instruction>();

                {
                    Instruction? inst = null;
                    while ((inst = Instruction.Read(chars, ref pos)) != null)
                    {
                        prgmTemp.Add(inst.Value);
                    }
                }

                prgm = prgmTemp.ToArray();
                prgmLen = prgm.Length;

                // Match loop starts and ends
                var starts = new Stack<int>();
                for(int i = 0; i < prgmLen; i++)
                {
                    if (prgm[i].op == Instruction.Op.LoopStart)
                    {
                        // Note the start position
                        starts.Push(i);
                    }
                    if (prgm[i].op == Instruction.Op.LoopEnd)
                    {
                        // Link the two brackets
                        var startInd = starts.Pop();
                        prgm[startInd].data = i;
                        prgm[i].data = startInd;
                    }
                }
            }

            // Main program loop
            int yieldTimer = 0;
            while(prgmPtr < prgmLen)
            {
                if(!dontYield && yieldTimer++ >= 1000000)
                {
                    yieldTimer = 0;
                    Thread.Sleep(0);
                }

                var inst = prgm[prgmPtr];
                switch(inst.op)
                {
                    case Instruction.Op.PtrAdd:
                        cellPtr += inst.data;
                        break;

                    case Instruction.Op.CellAdd:
                        tape.Add(cellPtr, inst.data);
                        break;

                    case Instruction.Op.Output:
                        char c = (char)(byte)tape.Get(cellPtr);
                        if (c == '\n')
                        {
                            GameConsole.WriteLineThreaded(output.ToString());
                            output = new StringBuilder();
                        }
                        else
                        {
                            output.Append(c);
                        }
                        break;

                    case Instruction.Op.Input:
                        if (localInput == null || localInput.Count == 0)
                            localInput = TakeInput();

                        tape.Set(cellPtr, localInput.Dequeue());
                        break;

                    case Instruction.Op.LoopStart:
                        if(tape.IsZero(cellPtr))
                            prgmPtr = inst.data;
                        break;

                    case Instruction.Op.LoopEnd:
                        if (!tape.IsZero(cellPtr))
                            prgmPtr = inst.data;
                        break;
                }

                prgmPtr++;
            }

            // Flush remaining output
            if (output.Length > 0)
                GameConsole.WriteLineThreaded(output.ToString());
        }

        private Queue<char> TakeInput()
        {
            bool wait = false;
            lock(inputLock)
            {
                wait = input == null || input.Count == 0;
            }

            // Wait until there's some input available
            if (wait)
                waitForInput.WaitOne();

            // Grab input
            lock (inputLock)
            {
                waitForInput.Reset();
                var temp = input;
                input = new Queue<char>();
                return temp;
            }
        }

        private abstract class Tape<T>
        {
            private bool allowNeg = true;
            private T[] positive;
            private T[] negative;

            public bool AllowNeg
            {
                get => allowNeg;
                set
                {
                    allowNeg = value;
                    if (!allowNeg)
                        negative = new T[0];
                }
            }

            public Tape()
            {
                positive = new T[64];
                negative = new T[64];
            }

            public void Add(int cell, int amount)
            {
                if (cell >= 0)
                {
                    if (cell >= positive.Length) Expand(ref positive, cell);
                    Add(positive, cell, amount);
                }
                else
                {
                    cell = -cell - 1;
                    if (cell >= negative.Length) Expand(ref negative, cell);
                    Add(negative, cell, amount);
                }
            }

            protected abstract void Add(T[] array, int cell, int amount);

            public char Get(int cell)
            {
                if (cell >= 0)
                {
                    if (cell >= positive.Length) Expand(ref positive, cell);
                    return Get(positive, cell);
                }
                else
                {
                    cell = -cell - 1;
                    if (cell >= negative.Length) Expand(ref negative, cell);
                    return Get(negative, cell);
                }
            }

            protected abstract char Get(T[] array, int cell);

            public void Set(int cell, char value)
            {
                if (cell >= 0)
                {
                    if (cell >= positive.Length) Expand(ref positive, cell);
                    Set(positive, cell, value);
                }
                else
                {
                    cell = -cell - 1;
                    if (cell >= negative.Length) Expand(ref negative, cell);
                    Set(negative, cell, value);
                }
            }

            protected abstract void Set(T[] array, int cell, char value);

            public bool IsZero(int cell)
            {
                if (cell >= 0)
                {
                    if (cell >= positive.Length) Expand(ref positive, cell);
                    return IsZero(positive, cell);
                }
                else
                {
                    cell = -cell - 1;
                    if (cell >= negative.Length) Expand(ref negative, cell);
                    return IsZero(negative, cell);
                }
            }

            protected abstract bool IsZero(T[] array, int cell);

            private void Expand(ref T[] array, int mustInclude)
            {
                if (array == negative)
                    throw new InvalidOperationException("This tape does not support negative cells!");
                Array.Resize(ref array, (mustInclude / Math.Max(array.Length, 1) + 1) * Math.Max(array.Length, 1));
            }
        }

        private class ByteTape : Tape<byte>
        {
            protected override void Add(byte[] array, int cell, int amount) => array[cell] += (byte)amount;

            protected override char Get(byte[] array, int cell) => (char)array[cell];

            protected override bool IsZero(byte[] array, int cell) => array[cell] == 0;

            protected override void Set(byte[] array, int cell, char value) => array[cell] = (byte)value;
        }

        private class ShortTape : Tape<ushort>
        {
            protected override void Add(ushort[] array, int cell, int amount) => array[cell] += (ushort)amount;

            protected override char Get(ushort[] array, int cell) => (char)array[cell];

            protected override bool IsZero(ushort[] array, int cell) => array[cell] == 0;

            protected override void Set(ushort[] array, int cell, char value) => array[cell] = value;
        }

        private class IntTape : Tape<uint>
        {
            protected override void Add(uint[] array, int cell, int amount) => array[cell] += (uint)amount;

            protected override char Get(uint[] array, int cell) => (char)array[cell];

            protected override bool IsZero(uint[] array, int cell) => array[cell] == 0;

            protected override void Set(uint[] array, int cell, char value) => array[cell] = value;
        }

        private class LongTape : Tape<ulong>
        {
            protected override void Add(ulong[] array, int cell, int amount) => array[cell] += (ulong)amount;

            protected override char Get(ulong[] array, int cell) => (char)array[cell];

            protected override bool IsZero(ulong[] array, int cell) => array[cell] == 0;

            protected override void Set(ulong[] array, int cell, char value) => array[cell] = value;
        }

        private struct Instruction
        {
            public Op op;
            public int data;

            public Instruction(Op op, int data = 0)
            {
                this.op = op;
                this.data = data;
            }

            private static readonly char[] validChars = new char[] {
                '<', '>', '+', '-', '.', ',', '[', ']'
            };
            public static Instruction? Read(char[] input, ref int pos)
            {
                while (pos < input.Length && Array.IndexOf(validChars, input[pos]) == -1) pos++;
                if (pos >= input.Length) return null;

                Op op;
                int data = 0;

                switch(input[pos])
                {
                    // Aggregate pointer movement instructions
                    case '>':
                    case '<':
                        op = Op.PtrAdd;
                        data = 0;
                        while(pos < input.Length)
                        {
                            char c = input[pos];
                            if (c == '<') data--;
                            else if (c == '>') data++;
                            else if (Array.IndexOf(validChars, c) != -1) break;
                            pos++;
                        }
                        break;

                    // Aggregate cell change instructions
                    case '+':
                    case '-':
                        op = Op.CellAdd;
                        data = 0;
                        while (pos < input.Length)
                        {
                            char c = input[pos];
                            if (c == '-') data--;
                            else if (c == '+') data++;
                            else if (Array.IndexOf(validChars, c) != -1) break;
                            pos++;
                        }
                        break;

                    // Simple ops
                    case '[': op = Op.LoopStart; pos++; break;
                    case ']': op = Op.LoopEnd; pos++; break;
                    case '.': op = Op.Output; pos++; break;
                    case ',': op = Op.Input; pos++;  break;

                    // Something went wrong
                    default:
                        return null;
                }

                return new Instruction(op, data);
            }

            public enum Op : byte
            {
                PtrAdd,
                CellAdd,
                Output,
                Input,
                LoopStart,
                LoopEnd
            }
        }

        public static void Undo() => QuitWatcher.Undo();

        private class QuitWatcher : MonoBehaviour
        {
            private static QuitWatcher instance;

            public static void Undo()
            {
                Destroy(instance.gameObject);
                instance = null;
            }

            public static event Action OnQuit
            {
                add
                {
                    if(instance == null)
                    {
                        GameObject go = new GameObject("Quit Watcher", typeof(QuitWatcher));
                        instance = go.GetComponent<QuitWatcher>();
                    }
                    OnQuitInternal += value;
                }
                remove
                {
                    OnQuitInternal -= value;
                    if(OnQuitInternal == null)
                    {
                        Undo();
                    }
                }
            }

            private static event Action OnQuitInternal;

#pragma warning disable IDE0051 // Remove unused private members
            private void OnApplicationQuit()
#pragma warning restore IDE0051 // Remove unused private members
            {
                OnQuitInternal?.Invoke();
            }
        }
    }
}
