using Financisto.Desktop.Models;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel; // Adicione isso para PropertyChangedEventArgs
using System.IO;
using System.Text.Json;

namespace Financisto.Desktop.Services
{
    public class TransactionsService
    {
        private const string ArquivoJson = "transacoes.json";

        public ObservableCollection<Transacao> Transactions { get; } = new();

        public TransactionsService()
        {
            Carregar();
            // Substituindo o CollectionChanged para salvar no arquivo sempre que houver uma alteração.
            Transactions.CollectionChanged += (_, _) => Salvar();
        }

        public void AdicionarTransacao(Transacao t)
        {
            if (t != null)
            {
                // Subscreve o PropertyChanged para salvar quando qualquer propriedade muda
                t.PropertyChanged += OnTransacaoPropertyChanged;
                Transactions.Add(t);
            }
        }

        public void RemoverTransacao(Transacao t)
        {
            if (t != null)
            {
                // Remove o handler para evitar memory leaks
                t.PropertyChanged -= OnTransacaoPropertyChanged;
                Transactions.Remove(t);
            }
        }

        public void ResetarTudo()
        {
            try
            {
                Transactions.CollectionChanged -= (_, _) => Salvar();

                // Remove handlers de todas as transações antes de limpar
                foreach (var t in Transactions)
                {
                    t.PropertyChanged -= OnTransacaoPropertyChanged;
                }

                Transactions.Clear();

                if (File.Exists(ArquivoJson))
                    File.Delete(ArquivoJson);

                Transactions.CollectionChanged += (_, _) => Salvar();
            }
            catch (Exception ex)
            {
                Console.WriteLine("Erro ao resetar dados: " + ex.Message);
            }
        }

        // Handler para mudanças em propriedades de Transacao
        private void OnTransacaoPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            // Salva sempre que qualquer propriedade muda (pode filtrar por propriedade se necessário, ex.: if (e.PropertyName == "Valor"))
            Console.WriteLine($"Propriedade '{e.PropertyName}' mudou em uma transação. Salvando...");
            Salvar();
        }

        // Método de salvar as transações no arquivo JSON
        public void Salvar()
        {
            try
            {
                Console.WriteLine("Tentando salvar as transações...");
                var options = new JsonSerializerOptions { WriteIndented = true };
                var json = JsonSerializer.Serialize(Transactions, options);
                File.WriteAllText(ArquivoJson, json);
                Console.WriteLine("Dados salvos com sucesso no arquivo JSON.");
            }
            catch (Exception ex)
            {
                Console.WriteLine("Erro ao salvar JSON: " + ex.Message);
            }
        }

        // Método de carregar as transações do arquivo JSON
        private void Carregar()
        {
            try
            {
                if (!File.Exists(ArquivoJson)) return;

                var json = File.ReadAllText(ArquivoJson);
                var lista = JsonSerializer.Deserialize<ObservableCollection<Transacao>>(json);

                if (lista == null) return;

                foreach (var t in lista)
                {
                    // Recria o comando de excluir
                    t.ExcluirCommand =
                        new CommunityToolkit.Mvvm.Input.RelayCommand(() => RemoverTransacao(t));

                    // Subscreve PropertyChanged para edições futuras
                    t.PropertyChanged += OnTransacaoPropertyChanged;

                    Transactions.Add(t);
                }
                Console.WriteLine($"Carregadas {lista.Count} transações do JSON.");
            }
            catch (Exception ex)
            {
                Console.WriteLine("Erro ao carregar JSON: " + ex.Message);
            }
        }
    }
}