/* ------------------------------------------ START OF LICENSE -----------------------------------------
* UncertainT
*
* Copyright(c) Microsoft Corporation
*
* All rights reserved.
*
* MIT License
*
* Permission is hereby granted, free of charge, to any person obtaining a copy
* of this software and associated documentation files (the ""Software""), to 
* deal in the Software without restriction, including without limitation the 
* rights to use, copy, modify, merge, publish, distribute, sublicense, and/or 
* sell copies of the Software, and to permit persons to whom the Software is 
* furnished to do so, subject to the following conditions:
*
* The above copyright notice and this permission notice shall be included in 
* all copies or substantial portions of the Software.
*
* THE SOFTWARE IS PROVIDED *AS IS*, WITHOUT WARRANTY OF ANY KIND, EXPRESS OR 
* IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, 
* FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE 
* AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER 
* LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, 
* OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE 
* SOFTWARE.
* ----------------------------------------------- END OF LICENSE ------------------------------------------
*/

using System;
using System.Collections.Generic;
using System.Linq;

namespace Microsoft.Research.Willis
{
    public class Compiler
    {
        private int groupcounter;
        private int labelcounter;

        public Compiler()
        {
            this.groupcounter = 0;
            this.labelcounter = 0;
        }
        public interface Bytecode : IEquatable<Bytecode> { }

        internal struct Match : Bytecode
        {
            public bool Equals(Bytecode other)
            {
                return other is Match;
            }
        }

        internal struct Label : Bytecode
        {
            internal int Location { get; set; }

            public bool Equals(Bytecode other)
            {
                if (other is Label)
                {
                    var tmp = (Label)other;
                    return tmp.Location == this.Location;
                }
                return false;
            }
        }
        internal struct Symbol : Bytecode
        {
            internal char Lo { get; set; }
            internal char Hi { get; set; }

            public bool Equals(Bytecode other)
            {
                if (other is Symbol)
                {
                    var tmp = (Symbol)other;
                    return tmp.Lo == this.Lo && this.Hi == this.Hi;
                }
                return false;
            }
        }
        internal struct Fork : Bytecode
        {
            internal Label Fst { get; set; }
            internal Label Snd { get; set; }
            internal bool Greedy { get; set; }


            public bool Equals(Bytecode other)
            {
                if (other is Fork)
                {
                    var tmp = (Fork)other;
                    return tmp.Fst.Equals(this.Fst) && tmp.Snd.Equals(this.Snd) && tmp.Greedy == this.Greedy;
                }
                return false;
            }
        }
        internal struct Jump : Bytecode
        {
            internal Label Where { get; set; }

            public bool Equals(Bytecode other)
            {
                if (other is Jump)
                {
                    var tmp = (Jump)other;
                    return tmp.Where.Equals(this.Where);
                }

                return false;
            }
        }
        internal struct Save : Bytecode
        {
            internal int Id { get; set; }

            public bool Equals(Bytecode other)
            {
                if (other is Save)
                {
                    var tmp = (Save)other;
                    return tmp.Id == this.Id;
                }
                return false;
            }
        }

        private IEnumerable<Bytecode> InternalCompile(Parser.RegularExpression e)
        {
            if (e is Parser.Primitive)
            {
                var prim = e as Parser.Primitive;
                yield return new Symbol { Lo = prim.Lo, Hi = prim.Hi };
            }
            else if (e is Parser.Sequence)
            {
                var seq = e as Parser.Sequence;
                var left = InternalCompile(seq.Left);
                var right = InternalCompile(seq.Right);
                foreach (var item in left.Concat(right)) yield return item;
            }
            else if (e is Parser.Group)
            {
                var group = e as Parser.Group;
                var id = this.groupcounter++;
                yield return new Save { Id = 2 * id };
                foreach (var item in InternalCompile(group.Re)) yield return item;
                yield return new Save { Id = 2 * id + 1 };
            }
            else if (e is Parser.Or)
            {
                var choice = e as Parser.Or;
                var l1 = new Label { Location = this.labelcounter++ };
                var l2 = new Label { Location = this.labelcounter++ };
                var l3 = new Label { Location = this.labelcounter++ };

                var e1 = InternalCompile(choice.Left);
                var e2 = InternalCompile(choice.Right);

                yield return new Fork { Fst = l1, Snd = l2 };
                yield return l1;
                foreach (var item in e1) yield return item;
                yield return new Jump { Where = l3 };
                yield return l2;
                foreach (var item in e2) yield return item;
                yield return l3;
            }
            else if (e is Parser.Star)
            {
                var star = e as Parser.Star;

                var l1 = new Label { Location = this.labelcounter++ };
                var l2 = new Label { Location = this.labelcounter++ };
                var l3 = new Label { Location = this.labelcounter++ };

                var e1 = InternalCompile(star.Re);

                yield return l1;
                if (star.Greedy)
                    yield return new Fork { Fst = l2, Snd = l3, Greedy = true };
                else
                    yield return new Fork { Fst = l3, Snd = l2, Greedy = false };
                yield return l2;
                foreach (var item in e1) yield return item;
                yield return new Jump { Where = l1 };
                yield return l3;
            }
            else if (e is Parser.Plus)
            {
                var plus = e as Parser.Plus;

                var l1 = new Label { Location = this.labelcounter++ };
                var l2 = new Label { Location = this.labelcounter++ };
                var l3 = new Label { Location = this.labelcounter++ };

                var e1 = InternalCompile(plus.Re);
                yield return l1;
                foreach (var item in e1) yield return item;
                if (plus.Greedy)
                    yield return new Fork { Fst = l1, Snd = l3, Greedy = true };
                else
                    yield return new Fork { Fst = l3, Snd = l1, Greedy = false };
                yield return l3;

            }
            else if (e is Parser.Question)
                throw new Exception("Finish types");
            yield break;
        }

        public IEnumerable<Bytecode> Compile(Parser.RegularExpression e)
        {
            this.groupcounter = 0;
            this.labelcounter = 0;
            foreach (var item in InternalCompile(e))
                yield return item;
            yield return new Match { };
        }


    }
}
