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
using Microsoft.Research.Uncertain;
using Microsoft.Research.Uncertain.Inference;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Microsoft.Research.Willis
{

    public static class Extensions
    {
        public static IEnumerable<IEnumerable<T>> CartesianProduct<T>(this IEnumerable<IEnumerable<T>> sequences)
        {
            IEnumerable<IEnumerable<T>> emptyProduct = new[] { Enumerable.Empty<T>() };
            return sequences.Aggregate(
                emptyProduct,
                (accumulator, sequence) =>
                    from accseq in accumulator
                    from item in sequence
                    select accseq.Concat(new[] { item })
                );
        }
    }

    class Program
    {
        static Uncertain<bool> Expertprior()
        {
            return new Flip(0.2);
        }
        // likelihood of utterance given whether speaker is an expert
        static double RegexpMeaning(string utterance, string derivation, bool isexpert)
        {

            if (isexpert)
                if (utterance == derivation)
                    return 0.99;
                else
                    return 0.01;

            if (utterance == derivation)
                return 0.8;
            return 0.2;
        }

        static IEnumerable<string> PossibleInterpretationsHelper(string input, int depth)
        {
            yield return input;
            if (depth == 0)
            {
                yield break;
            }

            var chars = Enumerable.Range('a', 'z' - 'a' + 1).Select(k => (char)k).ToList();
            //var chars = new FiniteEnumeration<char>(tmp);

            Func<string, IEnumerable<string>> insert = str =>
            {
                var positions = Enumerable.Range(0, str.Length).Where(k => Char.IsLetterOrDigit(str[k]));

                var strs = from pos in positions
                           from c in chars
                           from b in new[] { true, false }
                           let edit = b ? Char.ToUpper(c) : c
                           let copy = str.Insert(pos, edit.ToString())
                           where copy != input
                           select copy;
                return strs;
            };

            Func<string, IEnumerable<string>> delete = str =>
            {
                var strs = from pos in Enumerable.Range(0, str.Length)
                           where Char.IsLetterOrDigit(str[pos])
                           let copy = str.Remove(pos, 1)
                           where copy != input
                           select copy;
                return strs;
            };

            Func<string, IEnumerable<string>> substitute = str =>
            {
                var positions = Enumerable.Range(0, str.Length).Where(k => Char.IsLetterOrDigit(str[k]));
                var strs = from pos in positions
                           from c in chars
                           from b in new[] { true, false }
                           let edit = b ? Char.ToUpper(c) : c
                           let copy = str.Remove(pos, 1).Insert(pos, edit.ToString())
                           where copy != input
                           select copy;
                return strs;
            };

            Func<string, int, bool> IsGreedy = (str, starpos) =>
            {
                if (starpos + 1 == str.Length) return true; // last position is not a ?
                if (str[starpos + 1] == '?')
                    return false; // found a *? so this is a nongreedy match
                return true; // just a star in isolation
            };

            Func<string, IEnumerable<string>> makenongreedy = str =>
            {
                var starpos = str.Select((k, i) => Tuple.Create(k, i)).Where(i => i.Item1 == '*').Select(i => i.Item2).Where(k => IsGreedy(str, k)).ToList();
                var strs = from pos in starpos
                               //where IsGreedy(str, pos) == true
                           let copy = str.Remove(pos, 1).Insert(pos, "*?")
                           where copy != input
                           select copy;
                return strs;
            };

            Func<string, IEnumerable<string>> makegreedy = str =>
            {
                var starpos = str.Select((k, i) => Tuple.Create(k, i)).Where(i => i.Item1 == '*').Select(i => i.Item2).Where(k => !IsGreedy(str, k)).ToList();
                var strs = from pos in starpos
                               //where IsGreedy(str, pos) == false
                           let copy = str.Remove(pos, 2).Insert(pos, "*")
                           where copy != input
                           select copy;
                return strs;
            };

            var edits = new[] { insert, delete, substitute, makenongreedy, makegreedy };

            var output = from edit in edits
                         from editedinput in edit(input)
                         where Parser.IsExpression(editedinput)
                         select editedinput;

            var recurse = from edit in output
                          from next in PossibleInterpretationsHelper(edit, depth - 1)
                          select next;

            foreach (var item in recurse)
                yield return item;
        }

        static Uncertain<string> PossibleInterpretations2(string input)
        {
            var tmp = Enumerable.Range('a', 'z' - 'a' + 1).Select(k => (char)k)/*.Concat(new[] { '(', ')' })*/.ToList();
            var chars = new Multinomial<char>(tmp);

            Func<string, Uncertain<string>> insert = str =>
            {
                var strs = from pos in new FiniteEnumeration<int>(Enumerable.Range(0, str.Length).ToList())
                           where str[pos] != '('
                           where str[pos] != ')'
                           from c in chars
                           from b in new Flip(0.2)
                           let edit = b ? Char.ToUpper(c) : c
                           let copy = str.Insert(pos, edit.ToString())
                           where copy != input
                           select copy;

                //var foo = strs.Inference().Support().OrderByDescending(k => k.Probability).ToList();
                //return new FiniteEnumeration<string>(foo.Take((int)(foo.Count * 0.1)).ToList());
                return strs.Inference();
            };

            Func<string, Uncertain<string>> delete = str =>
            {
                var strs = from pos in new FiniteEnumeration<int>(Enumerable.Range(0, str.Length).ToList())
                           where str[pos] != '('
                           where str[pos] != ')'
                           let copy = str.Remove(pos, 1)
                           where copy != input
                           select copy;
                return strs.Inference();
            };

            Func<string, Uncertain<string>> substitute = str =>
            {
                var strs = from pos in new FiniteEnumeration<int>(Enumerable.Range(0, str.Length).ToList())
                           where str[pos] != '('
                           where str[pos] != ')'
                           from c in chars
                           from b in new Flip(0.2)
                           let edit = b ? Char.ToUpper(c) : c
                           let copy = str.Remove(pos, 1).Insert(pos, edit.ToString())
                           where copy != input
                           select copy;
                return strs.Inference();
            };

            var edits = new Multinomial<Func<string, Uncertain<string>>>(new[] { insert, delete, substitute });

            var output = from edit in edits
                         from editedinput in edit(input)
                         where editedinput != String.Empty && Parser.IsExpression(editedinput)
                         select editedinput;

            var recurse = from edit in output.Inference()
                          from b in new Flip(0.1)
                          let next = b ? PossibleInterpretations2(edit) : edit
                          from recursivelyedited in next
                          select recursivelyedited;
            return recurse;
        }

        static Uncertain<string> PossibleInterpretations3(IList<char> legalChars, string input, int depth, bool runInference = false)
        {
            if (depth == 0)
                return (Uncertain<string>) input;

            var chars = new Multinomial<char>(legalChars);

            Func<string, Uncertain<string>> insert = str =>
            {
                var strs = from pos in new FiniteEnumeration<int>(Enumerable.Range(0, str.Length).ToList())
                           where str[pos] != '('
                           where str[pos] != ')'
                           from c in chars
                           let copy = str.Insert(pos, c.ToString())
                           where copy != input
                           select copy;

                return strs;
            };

            Func<string, Uncertain<string>> delete = str =>
            {
                var strs = from pos in new FiniteEnumeration<int>(Enumerable.Range(0, str.Length).ToList())
                           where str[pos] != '('
                           where str[pos] != ')'
                           let copy = str.Remove(pos, 1)
                           where copy != input
                           select copy;
                return strs;
            };

            Func<string, Uncertain<string>> substitute = str =>
            {
                var strs = from pos in new FiniteEnumeration<int>(Enumerable.Range(0, str.Length).ToList())
                           where str[pos] != '('
                           where str[pos] != ')'
                           from c in chars
                           let copy = str.Remove(pos, 1).Insert(pos, c.ToString())
                           where copy != input
                           select copy;
                return strs;
            };

            var edits = new Multinomial<Func<string, Uncertain<string>>>(new[] { insert, delete, substitute });

            var output = from edit in edits
                         from editedinput in edit(input)
                         where editedinput != String.Empty && Parser.IsExpression(editedinput)
                         select editedinput;

            // toddm: recursive definition vs hard coded depth.  The latter works better for now.
            //var recurse = from edit in output.Inference()
            //              from b in new Flip(0.1)
            //              let next = b ? PossibleInterpretations3(legalChars, edit, depth, runInference) : edit
            //              from recursivelyedited in next
            //              select recursivelyedited;

            var recurse = from edit in output
                          from recursivelyedited in PossibleInterpretations3(legalChars, edit, depth - 1, runInference)
                          select recursivelyedited;

            return runInference ? recurse.Inference() : recurse;
        }



        static Uncertain<string> PossibleInterpretations(string input, int depth)
        {
            return new Multinomial<string>(PossibleInterpretationsHelper(input, depth).ToList());
        }

        static Uncertain<bool> LiteralCompiler(string utterance, string derivation)
        {
            var program = from isexpert in Expertprior()
                          let prob = RegexpMeaning(utterance, derivation, isexpert)
                          select new Weighted<bool>()
                          {
                              Value = isexpert,
                              Probability = prob
                          };
            return program.Inference();
        }

        static Uncertain<string> RegexpSpeaker(string utterance, bool isexpert)
        {
            var program = from derivation in PossibleInterpretations(utterance, isexpert ? 1 : 2) // all possible interpretations of regular expression
                          from expertise in LiteralCompiler(utterance, derivation) // infer expertise given statement
                          where expertise == isexpert
                          select derivation;
            return program.Inference();
        }

        static Uncertain<string> CompilerListener(string utterance)
        {
            var program = from isexpert in Expertprior() // prior over expertise
                          from stmt in RegexpSpeaker(utterance, isexpert) // how likely is statement (derived from utterance) given expertise
                          select stmt;

            return program.Inference();
        }

        static Uncertain<string> CompilerListenerWithExample(string utterance, IList<Tuple<string, IEnumerable<Tuple<int, int, int>>>> examples)//string input, IEnumerable<Tuple<int,int,int>> correct)
        {
            var tmp = new Multinomial<Tuple<string, IEnumerable<Tuple<int, int, int>>>>(examples);
            var program = from stmt in PossibleInterpretations2(utterance) //PossibleInterpretations(utterance, 2)
                          let re = new Parser(stmt).Parse()
                          let codes = new Compiler().Compile(re)
                          from example in tmp
                          let matches = new Interpreter().Run(codes.ToList(), example.Item1)
                          select new Weighted<string>
                          {
                              Value = stmt,
                              Probability = Cmp2(matches,stmt, example.Item2)
                          };

            return program.SampledInference(10000);
        }

        static HashSet<string> found;
        static Func<IEnumerable<Tuple<int, int, int>>, string, IEnumerable<Tuple<int, int, int>>, double> Cmp2 = (a, stmt, b) =>
        {
            double score = 0, tmpscore = 0, best = 0;
            foreach(var correct in b)
            {
                best += (correct.Item3 - correct.Item2);
                Tuple<int, int, int> guess = null;
                foreach (var tmp in a)
                {
                    if (tmp.Item1 == correct.Item1)
                    {
                        guess = tmp;
                        break;
                    }
                }

                if (guess == null)
                {
                    //score += 0.01; // add something because at least there is a match for this id in our guess
                    continue;
                }
                if (guess.Item3 < correct.Item2 || guess.Item2 > correct.Item3)
                {
                    //score += 0.01; // totally missed the boat
                    continue;
                }

                var st = Math.Max(correct.Item2, guess.Item2);
                var ed = Math.Min(correct.Item3, guess.Item3);
                var overlap = Math.Abs(ed - st);
                score += overlap / (double)(correct.Item3 - correct.Item2);
                tmpscore += overlap;
            }

            if (tmpscore == best)
            {
                found.Add(stmt);
                return 100000;
            }
            return score / best;
            //return Math.Exp(score/ best);
        };

        private static bool Score(IEnumerable<Tuple<int, int, int>> guesses, IEnumerable<Tuple<int, int, int>> corrects)
        {
            double score = 0, ideal = 0;
            foreach(var correct in corrects)
            {
                ideal += (correct.Item3 - correct.Item2);

                var guess = (from g in guesses where g.Item1 == correct.Item1 select g).FirstOrDefault();

                if (guess == null)
                {
                    continue;
                }

                if (guess.Item3 < correct.Item2 || guess.Item2 > correct.Item3)
                {
                    continue;
                }

                if (guess.Item3 < correct.Item2 || guess.Item2 > correct.Item3)
                {
                    //score += 0.01; // totally missed the boat
                    continue;
                }

                var st = Math.Max(correct.Item2, guess.Item2);
                var ed = Math.Min(correct.Item3, guess.Item3);
                var overlap = Math.Abs(ed - st);
                score += overlap; // / (double)(correct.Item3 - correct.Item2);
            }
            //if (score == ideal)
            //{
            //    int x = 10;
            //}
            return score == ideal;
        }

        static void Main(string[] args)
        {
            var test = "(aa*)";
            var parsed = new Parser(test).Parse();
            Console.Write("parsed: "+parsed+"\n");
            var compiled = new Compiler().Compile(parsed).ToList();
            foreach (var c in compiled)
            {
                Console.Write("compiled: " + c + "\n");
            }
            
            var output = new Interpreter().Run(compiled, "abc");
            foreach (var o in output)
            {
                Console.Write("output: " + o + "\n"); 
            }
           
            var examples = (from example in new[] { "aabbbbc", "abc", "aaaaaaaabbbbbbbc" }
                           let re = new Parser("(a*)(b*)(c)").Parse()
                           let codes = new Compiler().Compile(re).ToList()                           
                           let matches = new Interpreter().Run(codes, example)
                           select Tuple.Create(example, matches, example)).ToList();
            var example1 = examples[0];
            var example2 = examples[1];
            var example3 = examples[2];
  
            var interpreter = new Interpreter();
            
            var program = from stmt in PossibleInterpretations3(new[] { 'a', 'b', 'c', '.', '*' }, "(.)(.)(.)", 4, false)
                          where System.Text.RegularExpressions.Regex.IsMatch(stmt, "\\*\\*") == false // causes interpreter to go into infinite loop                    
                          let re = new Parser(stmt).Parse()
                          let codes = new Compiler().Compile(re).ToList()
                          where examples.Select(e => Score(new Interpreter().Run(codes, e.Item1), e.Item2)).All(score => score)
                          select stmt;
            Console.Write(program);
            //var tmpf = p.SampledInference(100000).Support().OrderByDescending(pp => pp.Probability).Take(20).ToList();
            var tmpf = program.Inference().Support().OrderByDescending(pp => pp.Probability).Take(20).ToList();
            foreach (var i in tmpf)
            {
                Console.WriteLine(String.Format("{0} {1}", i.Value, i.Probability));
            }
            Console.WriteLine();

           // foreach (var i in found)
            //{
              //  Console.WriteLine(i);
            //}

            //var sampler = new MarkovChainMonteCarloSampler<string>(p);
            //int count = 0;
            //foreach(var sample in sampler)
            //{                
            //    if (count++ % 1000 == 0)
            //    {
            //        Console.WriteLine(String.Format("{0} {1}", sample.Value, sample.Probability));
            //    }
            //}

            //var program = "(a.*)(a.*)(c)";
            //foreach (var item in RegexpSpeaker(program, true).Support().OrderByDescending(k => k.Probability).Take(5))
            //    Console.WriteLine(item);

            //Console.WriteLine();

            //foreach (var item in RegexpSpeaker(program, false).Support().OrderByDescending(k => k.Probability).Take(5))
            //    Console.WriteLine(item);

            //Console.WriteLine();

            //foreach (var item in CompilerListener(program).Support().OrderByDescending(k => k.Probability).Take(5))
            //    Console.WriteLine(item);

            //Console.WriteLine();

            //foreach (var item in CompilerListenerWithExample(program, examples).Support().OrderByDescending(k => k.Probability)) //;/.Take(5))
            //    Console.WriteLine(item);

            //var tmp = new Multinomial<Tuple<string, IEnumerable<Tuple<int, int, int>>>>(examples);

            //var output = sampler.Take(1).ToList();

            //var output = PossibleInterpretations2(program).SampledInference(100000).Support().OrderByDescending(k => k.Probability).Take(5).ToList();

            //Console.WriteLine();
            //var output = PossibleInterpretations2(program).SampledInference(100000).Support().OrderByDescending(k => k.Probability).Take(5);
            //foreach (var item in output)
            //    Console.WriteLine(item);

            int x = 10;
        }
    }
}
