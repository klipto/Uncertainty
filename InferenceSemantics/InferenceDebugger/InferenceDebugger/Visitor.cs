using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Research.Uncertain;
using Microsoft.Research.Uncertain.Inference;
using System.Linq.Expressions;
namespace InferenceDebugger
{
    public class Visitor: IUncertainVisitor
    {
        public List<SamplingInformation> Results;
        internal object sample;
        
        protected int generation;

        public Visitor()
        {
                       
        }
        public void Visit<TSource, TCollection, TResult>(SelectMany<TSource, TCollection, TResult> selectmany)
        {
            selectmany.source.Accept(this);
           
            if (this.sample.GetType().ToString().Contains("Microsoft.Research.Uncertain.Weighted"))
            {
                var a = (Weighted<TSource>)this.sample;
                Uncertain<TCollection> b = (selectmany.CollectionSelector.Compile())(a.Value);
                b.Accept(this);
                Weighted<TCollection> c = (Weighted<TCollection>)this.sample;
                Weighted<TResult> result = (selectmany.ResultSelector.Compile())(a.Value, c.Value);
                result.Probability = (a.Probability*c.Probability);
                this.sample = result.Value;
            }
        }

        public void Visit<TSource, TResult>(Select<TSource, TResult> select)
        {            
            select.source.Accept(this);
            var a = (Weighted<TSource>)this.sample;
            var b = select.Projection(a.Value);
            this.sample = new Weighted<TResult>(b.Value, a.Probability*b.Probability);
        }

        public void Visit<T>(Where<T> where)
        {
            where.source.Accept(this);
        }

        public void Visit<T>(RandomPrimitive<T> erp)
        {
            if (erp.GetType().ToString().Contains("FiniteEnumeration"))
            {
               // for (int x = ((Microsoft.Research.Uncertain.FiniteEnumeration<T>)erp).sampleMap.Count - 20;
                 //   x < ((Microsoft.Research.Uncertain.FiniteEnumeration<T>)erp).sampleMap.Count + 20; x+=5) {
                   //     erp.SampledInference(x);
               
               // }
            }
            //var sample = erp.Sample(this.generation++);
            //this.sample = (Weighted<T>)new Weighted<T>(sample);
        }

        public void Visit<T>(Inference<T> inference)
        {
            SamplingInformation sampling_information = new SamplingInformation(inference.GetType());           
            Console.Write("source type: "+inference.Source.GetType()+ "\n");
            inference.Source.Accept(this);
        }
    }
}
