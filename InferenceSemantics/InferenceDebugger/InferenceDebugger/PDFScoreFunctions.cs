using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace InferenceDebugger
{
    class PDFScoreFunctions
    {

        private static double GaussianScore(double t, double mu, double stdev)
        {
            var a = 1.0 / (stdev * Math.Sqrt(2 * Math.PI));
            var b = Math.Exp(-Math.Pow(t - mu, 2) / (2 * stdev * stdev));
            return a * b;
        }

        private static long Factorial(long n)
        {
            if (n <= 1)
                return 1;
            else
            {
                return n * Factorial(n - 1);
            }
        }
        private static double BinomialScore(int n, int r, double p)
        {
            var combination = Factorial(n) / (Factorial(r) * Factorial(n - r));
            return combination * Math.Pow(p, r) * Math.Pow((1 - p), (n - r));
        }

        private static double T_Score(double t, long dof)
        {
            double numerator = 1.0, denominator = 1.0;
            double ret = 0.0;
            double factor = Math.Pow((1 + (Math.Pow(t, 2) / dof)), (-(dof + 1) / 2));
            if (dof <= 3)
            {
                if (dof == 1)
                    ret = 1 / (Math.PI * (1 + Math.Pow(t, 2)));
                if (dof == 2)
                    ret = 1 / (Math.Pow((2 + Math.Pow(t, 2)), 3 / 2));
                if (dof == 3)
                    ret = 6 * Math.Sqrt(3) / (Math.PI * Math.Pow((3 + Math.Pow(t, 2)), 2));
                return ret;
            }
            else
            {
                if (dof % 2 == 0)
                {
                    for (int x = 3; x <= dof - 1; x += 2)
                    {
                        numerator = numerator * x;
                    }
                    for (int y = 2; y <= dof - 2; y += 2)
                    {
                        denominator = denominator * y;
                    }
                    ret = factor * numerator / (denominator * 2 * Math.Sqrt(dof));
                }
                else
                {
                    for (int x = 3; x <= dof - 2; x += 2)
                    {
                        denominator = denominator * x;
                    }
                    for (int y = 2; y <= dof - 1; y += 2)
                    {
                        numerator = numerator * y;
                    }
                    ret = factor * numerator / (denominator * Math.PI * Math.Sqrt(dof));
                }
                return ret;
            }
        }
    }
}
