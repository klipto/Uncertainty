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
using System.Diagnostics.Contracts;
using System.Linq;

namespace Microsoft.Research.Willis
{
    class Interpreter
    {
        public bool Recurse(IList<Compiler.Bytecode> codes,
            string input, IDictionary<int, int> labelMap,
            int pc, int sp,
            List<int> map)
        {
            while (true)
            {
                var code = codes[pc];

                if (code is Compiler.Match)
                {
                    // found one!
                    return true;
                }
                else if (code is Compiler.Label)
                {
                    pc++; // fall through to next opcode
                }
                else if (code is Compiler.Save)
                {
                    var g = (Compiler.Save)code;
                    // save old state
                    var old = map[g.Id];
                    // update map
                    map[g.Id] = sp;
                    if (Recurse(codes, input, labelMap, pc + 1, sp, map))
                        return true;
                    // restore old state because recursive call did
                    // not match
                    map[g.Id] = old;
                    return false;
                }
                else if (code is Compiler.Symbol)
                {
                    // end of input: can't match
                    if (sp == input.Length) return false;

                    var sym = (Compiler.Symbol)code;
                    var item = input[sp];
                    if ((item >= sym.Lo && item <= sym.Hi) == false)
                        return false;
                    pc++; // next opcode
                    sp++; // next input char
                }
                else if (code is Compiler.Jump)
                {
                    var jmp = (Compiler.Jump)code;
                    var loc = labelMap[jmp.Where.Location];
                    pc = loc;
                }
                else if (code is Compiler.Fork)
                {
                    var fork = (Compiler.Fork)code;
                    var loc1 = labelMap[fork.Fst.Location];
                    var loc2 = labelMap[fork.Snd.Location];

                    if (Recurse(codes, input, labelMap, loc1, sp, map))
                        return true;
                    pc = loc2;
                }
                else
                    throw new Exception("Unkown opcode");

            }
        }

        public IEnumerable<Tuple<int, int, int>> Run(IList<Compiler.Bytecode> codes, string input)
        {
            var labelMap = new Dictionary<int, int>();
            for(int idx = 0; idx < codes.Count; idx++)
            {
                var code = codes[idx];
                if (code is Compiler.Label)
                {
                    var loc = ((Compiler.Label)code).Location;
                    labelMap[loc] = idx;
                }
            }
            
            //var labelMap = codes.
            //    Select((code, idx) => new { code, idx }).
            //    Where(code => code.code is Compiler.Label).
            //    ToDictionary(k => ((Compiler.Label)k.code).Location, e => e.idx);

            var count = 1 + codes.Where(k => k is Compiler.Save).Select(k => ((Compiler.Save)k).Id).Max();
            Contract.Assert(count % 2 == 0);

            var matches = new List<int>();
            for (int i = 0; i < count; i++) matches.Add(-1);
            if (Recurse(codes, input, labelMap, 0, 0, matches))
                for (int id = 0; id < count / 2; id++)
                {
                    var start = 2 * id;
                    var end = 2 * id + 1;
                    if (matches[start] != -1 && matches[end] != -1)
                        yield return Tuple.Create(id, matches[start], matches[end]);
                }

            yield break;
        }
    }
}
