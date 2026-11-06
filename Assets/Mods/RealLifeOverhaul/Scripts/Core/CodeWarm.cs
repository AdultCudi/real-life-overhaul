using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;

namespace RealLifeOverhaul
{
    /// <summary>
    /// The mod's code for a moment that comes rarely (the game's midnight), compiled ahead a slice at a time, so its first
    /// run after a load does not pay Mono's compiler in one frame. Under Mono a method is compiled the first time it is
    /// called: the owner's log of 25 Oct had the mod's day at 148.7 ms in the first midnight after the load and its parts
    /// ten to twenty times smaller every midnight after (the company's rules 11.2 ms, then 0.4 ms, for the same work).
    /// From roots (the handlers on an event), each of the mod's methods is read for what it calls, makes or takes a delegate
    /// of, and the runtime is asked for its code (RuntimeMethodHandle.GetFunctionPointer compiles a method under Mono and
    /// runs nothing). The game's and the framework's methods met on the way are compiled but not read further. Nothing the
    /// code does changes, only when it is compiled. Game-free (only the runtime's reflection), checked by NavCheck.
    /// COST: what a slice is given, a method at a time (Core/WorkSlice); the methods seen are kept (some hundreds of
    /// kilobytes) so a method is never taken twice. Nothing while it is not asked to run.
    /// </summary>
    public sealed class CodeWarm
    {
        public const int HeavyIl = 4096;      // bytes of IL above which a method is compiled in a slice of its own (a large switch, a long day)
        private const int DelegateDepth = 3;  // delegates held inside a handler's closure looked into this deep (the guard's wrapper holds the handler)

        private static short[] _oneByte, _twoByte;   // the operand size of each opcode, -1 for a switch; made once from System.Reflection.Emit.OpCodes

        private readonly Module _own;
        private readonly int _most;
        private readonly int _deepest;
        private readonly int _heavy;
        private readonly Func<MethodBase, bool> _compile;
        private readonly HashSet<MethodBase> _seen = new HashSet<MethodBase>();
        private readonly List<MethodBase> _queue = new List<MethodBase>(256);
        private readonly List<int> _depths = new List<int>(256);   // calls from a root to each method in the queue
        private int _next;
        private byte[] _pendingIl;    // the IL of the method at _next, read before the slice was asked whether it may be done
        private bool _pendingRead;

        /// <summary>Methods of the mod read for what they call.</summary>
        public int Read { get; private set; }

        /// <summary>Methods whose code was asked for (the mod's, the game's and the framework's).</summary>
        public int Compiled { get; private set; }

        /// <summary>Methods or references the runtime refused (a member this game build lacks): left to be compiled when first called, as before.</summary>
        public int Refused { get; private set; }

        /// <summary>Methods met that have no code of their own to compile (abstract, open generic, the runtime's or a native call).</summary>
        public int Skipped { get; private set; }

        /// <summary>True when every method met has been read and compiled.</summary>
        public bool Done => _next >= _queue.Count;

        /// <summary>
        /// Methods of <paramref name="own"/> are read further, down to <paramref name="deepest"/> calls from a root; at most
        /// <paramref name="most"/> methods are taken in. <paramref name="compile"/> is how a method's code is asked for (the
        /// offline check counts instead); true when given. A method of more than <paramref name="heavyIl"/> bytes of IL is
        /// compiled only as the first of a slice.
        /// </summary>
        public CodeWarm(Module own, int most, int deepest, Func<MethodBase, bool> compile = null, int heavyIl = HeavyIl)
        {
            _own = own;
            _most = most;
            _deepest = deepest;
            _compile = compile ?? Compile;
            _heavy = heavyIl;
        }

        /// <summary>
        /// Every handler of an event, and the handlers its closures hold (Core/Guard's wrapper keeps the feature's handler
        /// in one). Read once, when the walk is set up: never per frame.
        /// </summary>
        public void Root(Delegate handlers)
        {
            Root(handlers, 0);
        }

        private void Root(Delegate handlers, int depth)
        {
            if (handlers == null || depth > DelegateDepth)
            {
                return;
            }
            Delegate[] list = handlers.GetInvocationList();
            for (int i = 0; i < list.Length; i++)
            {
                Delegate d = list[i];
                Take(d.Method, 0);
                object target = d.Target;
                if (target == null || target.GetType().Name.IndexOf('<') < 0)
                {
                    continue;   // only the compiler's closures: a feature's own fields are not the handler's code
                }
                FieldInfo[] fields = target.GetType().GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                for (int f = 0; f < fields.Length; f++)
                {
                    if (typeof(Delegate).IsAssignableFrom(fields[f].FieldType))
                    {
                        Root(fields[f].GetValue(target) as Delegate, depth + 1);
                    }
                }
            }
        }

        /// <summary>One method to start from.</summary>
        public void Root(MethodBase method)
        {
            Take(method, 0);
        }

        /// <summary>
        /// Reads and compiles methods while the slice allows, a method an item; a long method (by its IL) only as the first
        /// of a slice. True once all is done.
        /// </summary>
        public bool Run(ref WorkSlice slice)
        {
            while (_next < _queue.Count)
            {
                MethodBase method = _queue[_next];
                int depth = _depths[_next];
                if (!_pendingRead)
                {
                    _pendingIl = Body(method);   // for its length too: a long method at the deepest level is still compiled alone
                    _pendingRead = true;
                }
                if (!slice.More(_pendingIl != null && _pendingIl.Length > _heavy))
                {
                    return false;
                }
                byte[] il = _pendingIl;
                _pendingIl = null;
                _pendingRead = false;
                _next++;
                if (il != null && depth < _deepest)
                {
                    Read++;
                    Walk(method, il, depth + 1);
                }
                try
                {
                    if (_compile(method))
                    {
                        Compiled++;
                    }
                    else
                    {
                        Refused++;
                    }
                }
                catch (Exception)
                {
                    Refused++;   // a member this build of the game lacks: the method is compiled when first called, as before
                }
            }
            _queue.Clear();   // the methods seen are kept: a later load takes in only a handler not met before
            _depths.Clear();
            _next = 0;
            return true;
        }

        /// <summary>Asks the runtime for a method's code: under Mono that compiles it, and nothing is run.</summary>
        public static bool Compile(MethodBase method)
        {
            return method.MethodHandle.GetFunctionPointer() != IntPtr.Zero;
        }

        /// <summary>False for a method with no code of its own to compile: abstract, open generic, the runtime's own or a native call.</summary>
        public static bool HasCode(MethodBase method)
        {
            if (method == null || method.IsAbstract || method.ContainsGenericParameters)
            {
                return false;
            }
            if ((method.Attributes & MethodAttributes.PinvokeImpl) != 0)
            {
                return false;
            }
            MethodImplAttributes impl = method.GetMethodImplementationFlags();
            return (impl & (MethodImplAttributes.InternalCall | MethodImplAttributes.Runtime)) == 0;
        }

        private void Take(MethodBase method, int depth)
        {
            if (method == null || _seen.Count >= _most || !_seen.Add(method))
            {
                return;
            }
            if (!HasCode(method))
            {
                Skipped++;
                return;
            }
            _queue.Add(method);
            _depths.Add(depth);
        }

        // The IL of one of the mod's own methods; null for anything else (compiled, not read).
        private byte[] Body(MethodBase method)
        {
            if (method.Module != _own)
            {
                return null;
            }
            try
            {
                MethodBody body = method.GetMethodBody();
                return body?.GetILAsByteArray();
            }
            catch (Exception)
            {
                return null;   // compiled without being read; the compiler says if it refuses it
            }
        }

        // Every method the IL calls, makes (newobj) or takes the address of (a delegate's target); a type of the compiler's
        // that it makes (a coroutine's or a closure's) is taken whole, as its methods are called through an interface.
        private void Walk(MethodBase method, byte[] il, int depth)
        {
            Tables();
            Type[] typeArgs = method.DeclaringType != null && method.DeclaringType.IsGenericType ? method.DeclaringType.GetGenericArguments() : null;
            Type[] methodArgs = method.IsGenericMethod ? method.GetGenericArguments() : null;
            int at = 0;
            while (at < il.Length)
            {
                int code = il[at++];
                int size;
                bool calls = false;
                bool makes = false;
                if (code == 0xFE)
                {
                    if (at >= il.Length)
                    {
                        return;
                    }
                    int second = il[at++];
                    size = _twoByte[second];
                    calls = second == 0x06 || second == 0x07;   // ldftn, ldvirtftn
                }
                else
                {
                    size = _oneByte[code];
                    calls = code == 0x28 || code == 0x6F || code == 0x27;   // call, callvirt, jmp
                    makes = code == 0x73;                                   // newobj
                }
                if (size < 0)
                {
                    if (at + 4 > il.Length)
                    {
                        return;
                    }
                    int cases = BitConverter.ToInt32(il, at);
                    size = 4 + 4 * Math.Max(0, cases);   // switch: a count and that many targets
                }
                if ((calls || makes) && at + 4 <= il.Length)
                {
                    Met(BitConverter.ToInt32(il, at), typeArgs, methodArgs, makes, depth);
                }
                at += size;
            }
        }

        private void Met(int token, Type[] typeArgs, Type[] methodArgs, bool makes, int depth)
        {
            MethodBase callee;
            try
            {
                callee = _own.ResolveMethod(token, typeArgs, methodArgs);
            }
            catch (Exception)
            {
                Refused++;   // a member this build of the game lacks
                return;
            }
            Take(callee, depth);
            Type made = makes ? callee?.DeclaringType : null;
            if (made != null && made.Module == _own && made.Name.IndexOf('<') >= 0)
            {
                MethodInfo[] methods = made.GetMethods(BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
                for (int i = 0; i < methods.Length; i++)
                {
                    Take(methods[i], depth);
                }
            }
        }

        // The operand size of every opcode, from the framework's own list of them.
        private static void Tables()
        {
            if (_oneByte != null)
            {
                return;
            }
            var one = new short[256];
            var two = new short[256];
            FieldInfo[] fields = typeof(OpCodes).GetFields(BindingFlags.Public | BindingFlags.Static);
            for (int i = 0; i < fields.Length; i++)
            {
                if (!(fields[i].GetValue(null) is OpCode op))
                {
                    continue;
                }
                short size = OperandSize(op.OperandType);
                ushort value = unchecked((ushort)op.Value);
                if (op.Size == 1)
                {
                    one[value & 0xFF] = size;
                }
                else
                {
                    two[value & 0xFF] = size;
                }
            }
            _twoByte = two;
            _oneByte = one;
        }

        private static short OperandSize(OperandType type)
        {
            switch (type)
            {
                case OperandType.InlineNone:
                    return 0;
                case OperandType.ShortInlineBrTarget:
                case OperandType.ShortInlineI:
                case OperandType.ShortInlineVar:
                    return 1;
                case OperandType.InlineVar:
                    return 2;
                case OperandType.InlineI8:
                case OperandType.InlineR:
                    return 8;
                case OperandType.InlineSwitch:
                    return -1;
                default:
                    return 4;   // a branch, a token, a 32-bit number or a short real
            }
        }
    }
}
