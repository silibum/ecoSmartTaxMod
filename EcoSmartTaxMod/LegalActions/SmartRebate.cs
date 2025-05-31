﻿using System.Linq;
using System.Collections.Generic;

namespace Eco.Mods.SmartTax
{
    using Core.Utils;
    using Core.Controller;
    using Core.Utils.PropertyScanning;

    using Shared.Utils;
    using Shared.Networking;
    using Shared.Localization;

    using Gameplay.Civics;
    using Gameplay.Aliases;
    using Gameplay.Economy;
    using Gameplay.Players;
    using Gameplay.GameActions;
    using Gameplay.Settlements;
    using Gameplay.Civics.Laws;
    using Gameplay.Economy.Transfer;
    using Gameplay.Civics.GameValues;
    using Gameplay.Systems.TextLinks;
    using Gameplay.Civics.Laws.ExecutiveActions;

    [Eco, LocCategory("Finance"), CreateComponentTabLoc("Smart Tax", IconName = "Tax"), LocDisplayName("Smart Rebate"), HasIcon("Tax_LegalAction"), LocDescription("Issues a rebate which is used to forgive some amount of future or present tax debt.")]
    public class SmartRebate_LegalAction : LegalAction, ICustomValidity, IExecutiveAction
    {
        [Eco, LocDescription("Rebates taxes towards this account. Only Government Accounts are allowed."), GovernmentAccountsOnly]
        public GameValue<BankAccount> TargetBankAccount { get; set; } = MakeGameValue.Treasury;

        [Eco, Advanced, LocDescription("Which currency the rebate is for.")]
        public GameValue<Currency> Currency { get; set; }

        [Eco, LocDescription("The amount that is going to be deducted from taxes.")]
        public GameValue<float> Amount { get; set; } = MakeGameValue.GameValue(0f);

        [Eco, Advanced, LocDescription("The player or group to issue to the rebate to.")]
        public GameValue<IAlias> Target { get; set; }

        [Eco, LocDescription("A custom name for the rebate. If left blank, the name of the law will be used instead."), AllowNullInView]
        public string RebateCode { get; set; }

        [Eco, LocDescription("If true, no notification will be published at all when the rebate is applied. Will still notify when the tax is collected. Useful for high-frequency events like placing blocks or emitting pollution.")]
        public GameValue<bool> Silent { get; set; } = new No();

        public override LocString Description()
            => Localizer.Do($"Issue rebate of {Text.Currency(this.Amount.DescribeNullSafe())} {this.Currency.DescribeNullSafe()} from {this.Target.DescribeNullSafe()} into {this.TargetBankAccount.DescribeNullSafe()}.");
        protected override PostResult Perform(Law law, GameAction action, AccountChangeSet acc) => this.Do(law.UILinkNullSafe(), action, law?.Settlement);
        PostResult IExecutiveAction.PerformExecutiveAction(User user, IContextObject context, Settlement jurisdictionSettlement, AccountChangeSet acc) => this.Do(Localizer.Do($"Executive Action by {(user is null ? Localizer.DoStr("the Executive Office") : user.UILink())}"), context, jurisdictionSettlement);
        Result ICustomValidity.Valid() => this.Amount is GameValueWrapper<float> val && val.Object == 0f ? Result.Localize($"Must have non-zero value for amount.") : Result.Succeeded;

        private PostResult Do(LocString description, IContextObject context, Settlement jurisdictionSettlement)
        {
            var targetBankAccount = this.TargetBankAccount?.Value(context).Val;
            var currency = this.Currency?.Value(context).Val;
            var amount = this.Amount?.Value(context).Val ?? 0.0f;
            var alias = this.Target?.Value(context).Val;
            var rebateCode = string.IsNullOrEmpty(this.RebateCode) ? description : this.RebateCode;
            var silent = this.Silent?.Value(context).Val ?? false;

            if (currency == null) { return new PostResult($"Transfer currency must be set.", true); }
            if (targetBankAccount == null) { return new PostResult($"Target bank account must be set.", true); }
            if (alias == null) { return new PostResult($"Rebate without target citizen skipped.", true); }

            var jurisdiction = Jurisdiction.FromContext(context, jurisdictionSettlement);
            if (!jurisdiction.TestAccount(targetBankAccount)) { return new PostResult($"{targetBankAccount.MarkedUpName} isn't a government account of {jurisdiction} or held by any of its citizens.", true); }
            var users = jurisdiction.GetAllowedUsersFromTarget(context, alias, out var jurisdictionDescription, "rebated");
            if (!users.Any()) { return new PostResult(jurisdictionDescription, true); }

            if (silent)
            {
                return new PostResult(() =>
                {
                    RecordRebateForUsers(jurisdiction.Settlement, users, targetBankAccount, currency, rebateCode, amount);
                });
            }
            return new PostResult(() =>
            {
                RecordRebateForUsers(jurisdiction.Settlement, users, targetBankAccount, currency, rebateCode, amount);
                return Localizer.Do($"Issuing rebate of {currency.UILinkContent(amount)} from {alias.UILinkGeneric()} to {DescribeTarget(jurisdiction, targetBankAccount)} ({rebateCode})");
            });
        }

        private void RecordRebateForUsers(Settlement settlement, IEnumerable<User> users, BankAccount targetBankAccount, Currency currency, string rebateCode, float amount)
        {
            foreach (var user in users)
            {
                var taxCard = TaxCard.GetOrCreateForUser(user);
                taxCard.RecordRebate(settlement, targetBankAccount, currency, rebateCode, amount);
            }
        }

        private static LocString DescribeTarget(Jurisdiction jurisdiction, BankAccount targetAccount)
            => jurisdiction.IsGlobal ? targetAccount.UILink() : Localizer.Do($"{jurisdiction.Settlement.UILinkNullSafe()} ({targetAccount.UILink()})");
    }
}
