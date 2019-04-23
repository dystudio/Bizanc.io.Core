using System;
using System.Numerics;
using System.Threading.Tasks;
using Bizanc.io.Matching.Core.Domain;
using Bizanc.io.Matching.Core.Connector;
using Bizanc.io.Matching.Core.Util;
using System.Threading;
using NBitcoin;
using System.Text;
using System.Linq;
using System.Collections.Generic;
using NBitcoin.Policy;
using NBXplorer;
using NBXplorer.Models;

namespace Bizanc.io.Matching.Infra.Connector
{
    public class BitcoinOracleConnector
    {
        private BitcoinSecret wallet = new BitcoinSecret("cUJ9aAeteswkQXK3XXb8SMUxsbNBxCFJurS9SUqKRdaNC7qA6PL1");

        private ExplorerClient client = new ExplorerClient(new NBXplorerNetworkProvider(NetworkType.Testnet).GetBTC(), new Uri("http://seed.bizanc.io:24445"));

        public async Task<WithdrawInfo> WithdrawBtc(string withdrawHash, string recipient, decimal amount)
        {
            var txOperations = await client.GetUTXOsAsync(TrackedSource.Create(wallet.GetAddress()));

            var coins = new List<Coin>();

            foreach (var op in txOperations.Confirmed.UTXOs)
                coins.Add(op.AsCoin());

            coins.Sort(delegate (Coin x, Coin y)
            {
                return -x.Amount.CompareTo(y.Amount);
            });

            var coinSum = 0m;

            for (int i = 0; coinSum < amount; i++)
            {
                coinSum += coins[i].Amount.ToDecimal(MoneyUnit.BTC);
                if (coinSum >= amount)
                    coins.RemoveRange(i + 1, coins.Count - (i + 1));
            }

            var builder = Network.TestNet.CreateTransactionBuilder();
            var destination = new BitcoinPubKeyAddress(recipient, Network.TestNet);
            NBitcoin.Transaction tx = builder
                                .AddCoins(coins)
                                .AddKeys(wallet)
                                .Send(destination, Money.Coins(amount))
                                .Send(TxNullDataTemplate.Instance.GenerateScriptPubKey(Encoding.UTF8.GetBytes(withdrawHash)), Money.Zero)
                                .SetChange(wallet)
                                .SendFees(Money.Coins(0.0001m))
                                .BuildTransaction(sign: true);

            TransactionPolicyError[] errors = null;
            if (builder.Verify(tx, out errors))
            {
                var broadcastResult = await client.BroadcastAsync(tx);
                broadcastResult.ToString();
                return new WithdrawInfo() { TxHash = tx.GetHash().ToString(), Timestamp = DateTime.Now };
            }

            if (errors != null)
            {
                errors.ToString();
            }

            return null;
        }
    }
}