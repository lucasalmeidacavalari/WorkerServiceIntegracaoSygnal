using System;

namespace WorkerServiceIntegracaoSygnal.Dominio
{
    public class Sygnal
    {
        public string Symbol { get; }
        public string Name { get; }
        public string Model { get; }
        public string SignalValue { get; }
        public string SignalText { get; }
        public string PreviousValue { get; }
        public string Previous7d { get; }
        public string Updated { get; }

        public Sygnal(string symbol, string name, string model, string signalValue, string signalText, string previousValue, string previous7d, string updated)
        {
            Symbol = symbol;
            Name = name;
            Model = model;
            SignalValue = signalValue;
            SignalText = signalText;
            PreviousValue = previousValue;
            Previous7d = previous7d;
            Updated = updated;
        }

        public override string ToString()
        {
            return $"📌 {Symbol} | {Name} | Model: {Model} | Signal: {SignalValue} ({SignalText}) | Previous: {PreviousValue} / {Previous7d} | Updated: {Updated}";
        }
    }

}
