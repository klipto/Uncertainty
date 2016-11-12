using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.Linq;
using System.Text;
using System.IO;
using System.Threading.Tasks;

using Microsoft.Research.Uncertain;
using Microsoft.Research.Uncertain.Inference;

namespace DependenceAnalysis
{
	public class Temperature_Humidity
	{

		public List<double> temperature;
		public List<double> humidity;

		public List<double> out_temperature;
		public List<double> out_humidity;

		public List<Uniform<double>> temperature_distribution;
		public List<Uniform<double>> humidity_distribution;

		public Temperature_Humidity ()
		{
			temperature = new List<double> ();
			humidity = new List<double> ();

			out_temperature = new List<double> ();
			out_humidity = new List<double> ();

			temperature_distribution = new List<Uniform<double>> ();
			humidity_distribution = new List<Uniform<double>> ();
		}

		public void Parser(List<double> temperature, List<double> humidity) {
		
			StreamReader file = new StreamReader("temp_hum1.txt");
			string line;

			while ((line = file.ReadLine())!= null) {			
				double temp = Convert.ToDouble(line.Split(null).ElementAt(0).Trim());
				double hum = Convert.ToDouble (line.Split(null).ElementAt(1).Trim());
				temperature.Add (temp);
				humidity.Add (hum);
			}
		}

		public void ModifySensorData() {
			Parser (temperature, humidity);

			if (temperature.Count == humidity.Count) {
				for (int x=0; x < temperature.Count; x++) {
					int b =ChannelNoiseModel (temperature.ElementAt(x), humidity.ElementAt(x));
					UseSensorData (x, b);
				}
			}
		}

		public void UseSensorData(int data_number, int bit) {

			AddNoiseAt(bit, temperature.ElementAt (data_number), humidity.ElementAt (data_number));

			var t = ConvertDoubleToBinary (temperature.ElementAt (data_number));
			var h = ConvertDoubleToBinary (humidity.ElementAt (data_number));


			if (t[4] == '0' && t[0] == '0') {

				var max = FlipBit(temperature.ElementAt(data_number), 4);
				var min = FlipBit(temperature.ElementAt(data_number), 0);
				if (max >= min) {
					temperature_distribution.Add (new Uniform<double> (min, max));
				} else {
					temperature_distribution.Add (new Uniform<double> (max, min));
				}

			}

			else if (t[4] == '0' && t[0] == '1') {

				var max = FlipBit(temperature.ElementAt(data_number), 4);
				var min = FlipBit(temperature.ElementAt(data_number), 0);
				if (max >= min) {
					temperature_distribution.Add (new Uniform<double> (min, max));
				} else {
					temperature_distribution.Add (new Uniform<double> (max, min));
				}

			}

			else if (t[4] == '1' && t[0] == '0') {

				var min = FlipBit(temperature.ElementAt(data_number), 4);
				var max = FlipBit(temperature.ElementAt(data_number), 0);;
				if (max >= min) {
					temperature_distribution.Add (new Uniform<double> (min, max));
				} else {
					temperature_distribution.Add (new Uniform<double> (max, min));
				}

			}
			
			else if (t[4] == '1' && t[0] == '1') {

				var min = FlipBit(temperature.ElementAt(data_number), 4);
				var max = FlipBit(temperature.ElementAt(data_number), 0);
				if (max >= min) {
					temperature_distribution.Add (new Uniform<double> (min, max));
				} else {
					temperature_distribution.Add (new Uniform<double> (max, min));
				}

			}

			if (h[4] == '0' && h[0] == '0') {

				var max = FlipBit(humidity.ElementAt(data_number), 4);
				var min = FlipBit(humidity.ElementAt(data_number), 0);;
				if (max >= min) {
					humidity_distribution.Add (new Uniform<double> (min, max));
				} else {
					humidity_distribution.Add (new Uniform<double> (max, min));
				}

			}

			else if (h[4] == '0' && h[0] == '1') {

				var max = FlipBit(humidity.ElementAt(data_number), 4);
				var min = FlipBit(humidity.ElementAt(data_number), 0);
				if (max >= min) {
					humidity_distribution.Add (new Uniform<double> (min, max));
				} else {
					humidity_distribution.Add (new Uniform<double> (max, min));
				}

			}

			else if (h[4] == '1' && h[0] == '0') {

				var min = FlipBit(humidity.ElementAt(data_number), 4);
				var max = FlipBit(humidity.ElementAt(data_number), 0);
				if (max >= min) {
					humidity_distribution.Add (new Uniform<double> (min, max));
				} else {
					humidity_distribution.Add (new Uniform<double> (max, min));
				}
			}

			else if (h[4] == '1' && h[0] == '1') {

				var min = FlipBit(humidity.ElementAt(data_number), 4);
				var max = FlipBit(humidity.ElementAt(data_number), 0);
				if (max >= min) {
					humidity_distribution.Add (new Uniform<double> (min, max));
				} else {
					humidity_distribution.Add (new Uniform<double> (max, min));
				}	
			}
		} 

		public Uncertain<double> UncertainProgram() {
			ModifySensorData ();
			List<Uncertain<double>> ps = new List<Uncertain<double>> ();
			foreach (var t in temperature_distribution) {
				foreach (var h in humidity_distribution) {
					var p = function(t,h);
					ps.Add (p);
				}
			}
			return ps.ElementAt(0);
		}

		public Uncertain<double> function (Uncertain<double>t, Uncertain<double> h) {
			// TODO: doesn't really matter what happens here.
			var v = from tt in t
					from hh in h
					select (tt + hh);
			return v;
		}

		public void AddNoiseAt(int bit, double temperature, double humidity) {					
			//Console.WriteLine ("temp ");
			out_temperature.Add (FlipBit (temperature, bit));
			//Console.WriteLine ("hum ");
			out_humidity.Add( FlipBit(humidity, bit));
			//Console.WriteLine ("\n");
		}

		public double FlipBit(double v, int bit) {
				
			var bits = ConvertDoubleToBinary (v);

			//Console.WriteLine ("old: " + ConvertBinaryToDouble(new string(bits)));

			// flip the bit
			if (bits [bit] == '0') {
				bits [bit] = '1';
			} else if (bits [bit] == '1') {
				bits [bit] = '0';
			}

			// store the new number. 
			string new_number = new string(bits);
			var number = ConvertBinaryToDouble (new_number);

			//Console.WriteLine ("new: " + number);

			return number;
		}

		public int ChannelNoiseModel(double t, double h) {
			//double is represented using 64 bits
			//double probability = (double)1 / (double)63;

			double probability = (double)1 / (double)5;

			// pick an index to flip the bit in the actual sensor reading uniformly.
			List<Weighted<Int32>> indices = new List<Weighted<int>> (new Weighted<int>[]{
				new Weighted<int>(0, probability),new Weighted<int>(1, probability),new Weighted<int>(2, probability),new Weighted<int>(3, probability),new Weighted<int>(4, probability)
				//new Weighted<int>(5, probability),new Weighted<int>(6, probability),new Weighted<int>(7, probability),new Weighted<int>(8, probability),new Weighted<int>(9, probability),
				//new Weighted<int>(10, probability),new Weighted<int>(11, probability),new Weighted<int>(12, probability),new Weighted<int>(13, probability),new Weighted<int>(14, probability),
				//new Weighted<int>(15, probability),new Weighted<int>(16, probability),new Weighted<int>(17, probability),new Weighted<int>(18, probability),new Weighted<int>(19, probability),
				//new Weighted<int>(20, probability),new Weighted<int>(21, probability),new Weighted<int>(22, probability),new Weighted<int>(23, probability),new Weighted<int>(24, probability),
				//new Weighted<int>(25, probability),new Weighted<int>(26, probability),new Weighted<int>(27, probability),new Weighted<int>(28, probability),new Weighted<int>(29, probability),
				//new Weighted<int>(30, probability),new Weighted<int>(31, probability),new Weighted<int>(32, probability),new Weighted<int>(33, probability),new Weighted<int>(34, probability),
				//new Weighted<int>(35, probability),new Weighted<int>(36, probability),new Weighted<int>(37, probability),new Weighted<int>(38, probability),new Weighted<int>(39, probability),
				//new Weighted<int>(40, probability),new Weighted<int>(41, probability),new Weighted<int>(42, probability),new Weighted<int>(43, probability),new Weighted<int>(44, probability),
				//new Weighted<int>(45, probability),new Weighted<int>(46, probability),new Weighted<int>(47, probability),new Weighted<int>(48, probability),new Weighted<int>(49, probability),
				//new Weighted<int>(50, probability),new Weighted<int>(51, probability),new Weighted<int>(52, probability),new Weighted<int>(53, probability),new Weighted<int>(54, probability),
				//new Weighted<int>(55, probability),new Weighted<int>(56, probability),new Weighted<int>(57, probability),new Weighted<int>(58, probability),new Weighted<int>(59, probability),
				//new Weighted<int>(60, probability),new Weighted<int>(61, probability),new Weighted<int>(62, probability)
			});

			FiniteEnumeration<int> bit_to_be_flipped = new FiniteEnumeration<int> (indices);

			// pick a bit uniformly to flip.
			int bit = bit_to_be_flipped.SampledInference (1).Support ().ToList ().ElementAt (0).Value;
			return bit;
		}

		public char[] ConvertDoubleToBinary(double value) {

			long val = BitConverter.DoubleToInt64Bits (value);
		
			string binary = Convert.ToString (val, 2);
			var intermediate = binary.ToCharArray();

			char[] bits = new char[intermediate.Count()];

			for (int x=0; x<intermediate.Count(); x++) {
				bits[x] = Convert.ToChar(intermediate [x].ToString ().Split ('\'').ElementAt (0).Trim());
			}

			return bits;
		}

		public double ConvertBinaryToDouble(string str){
	    	long v = 0;

			for (int i = str.Length - 1; i >= 0; i--) {
				v = (v << 1) + (str[i] - '0');
			}

			double d = BitConverter.ToDouble(BitConverter.GetBytes(v), 0);
			return d;
		}
	}
}

