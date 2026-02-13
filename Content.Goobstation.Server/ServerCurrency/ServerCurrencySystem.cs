// SPDX-FileCopyrightText: 2024 Aiden <aiden@djkraz.com>
// SPDX-FileCopyrightText: 2025 Aiden <28298836+Aidenkrz@users.noreply.github.com>
// SPDX-FileCopyrightText: 2025 GoobBot <uristmchands@proton.me>
// SPDX-FileCopyrightText: 2025 Milon <milonpl.git@proton.me>
// SPDX-FileCopyrightText: 2025 Misandry <mary@thughunt.ing>
// SPDX-FileCopyrightText: 2025 SX-7 <92227810+SX-7@users.noreply.github.com>
// SPDX-FileCopyrightText: 2025 Tim <timfalken@hotmail.com>
// SPDX-FileCopyrightText: 2025 gluesniffler <159397573+gluesniffler@users.noreply.github.com>
// SPDX-FileCopyrightText: 2025 gus <august.eymann@gmail.com>
//
// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Goobstation.Common.CCVar;
using Content.Goobstation.Common.ServerCurrency;
using Content.Server._RMC14.LinkAccount;
using Content.Server.GameTicking;
using Content.Server.Popups;
using Content.Shared.Humanoid;
using Content.Shared.Mind;
using Content.Shared.Mind.Components;
using Content.Shared.Popups;
using Content.Shared.Roles.Jobs;
using Content.Shared.Silicons.Borgs.Components;
using Robust.Server.Player;
using Robust.Shared.Configuration;

namespace Content.Goobstation.Server.ServerCurrency
{
    /// <summary>
    /// Connects <see cref="ServerCurrencyManager"/> to the simulation state.
    /// </summary>
    public sealed class ServerCurrencySystem : EntitySystem
    {
        [Dependency] private readonly ICommonCurrencyManager _currencyMan = default!;
        [Dependency] private readonly PopupSystem _popupSystem = default!;
        [Dependency] private readonly SharedMindSystem _mind = default!;
        [Dependency] private readonly SharedJobSystem _jobs = default!;
        [Dependency] private readonly IPlayerManager _players = default!;
        [Dependency] private readonly IConfigurationManager _cfg = default!;
        [Dependency] private readonly LinkAccountManager _linkAccount = default!;
        [Dependency] private readonly GameTicker _gameTicker = default!;

        private int _goobcoinsPerPlayer = 10;
        private int _goobcoinsNonAntagMultiplier = 1;
        private int _goobcoinsServerMultiplier = 1;
        private int _goobcoinsMinPlayers;
        private bool _goobcoinsUseLowPopMultiplier;
        private double _goobcoinsLowPopMultiplierStrength = 1.0;
        private bool _goobcoinsUseShortRoundPenalty = true;
        private int _goobcoinsShortRoundPenaltyTargetMinutes = 50;


        private int _testcoinsPerPlayer = 10;
        private int _testcoinsNonAntagMultiplier = 1;
        private int _testcoinsServerMultiplier = 1;
        private int _testcoinsMinPlayers;
        private bool _testcoinsUseLowPopMultiplier;
        private double _testcoinsLowPopMultiplierStrength = 1.0;
        private bool _testcoinsUseShortRoundPenalty = true;
        private int _testcoinsShortRoundPenaltyTargetMinutes = 50;

        public override void Initialize()
        {
            base.Initialize();
            _currencyMan.BalanceChange += OnBalanceChange;
            SubscribeLocalEvent<RoundEndTextAppendEvent>(OnRoundEndText);
            SubscribeNetworkEvent<PlayerBalanceRequestEvent>(OnBalanceRequest);
            Subs.CVar(_cfg, GoobCVars.GoobcoinsPerPlayer, value => _goobcoinsPerPlayer = value, true);
            Subs.CVar(_cfg, GoobCVars.GoobcoinNonAntagMultiplier, value => _goobcoinsNonAntagMultiplier = value, true);
            Subs.CVar(_cfg, GoobCVars.GoobcoinServerMultiplier, value => _goobcoinsServerMultiplier = value, true);
            Subs.CVar(_cfg, GoobCVars.GoobcoinMinPlayers, value => _goobcoinsMinPlayers = value, true);
            Subs.CVar(_cfg, GoobCVars.GoobcoinUseLowpopMultiplier, value => _goobcoinsUseLowPopMultiplier = value, true);
            Subs.CVar(_cfg, GoobCVars.GoobcoinLowpopMultiplierStrength, value => _goobcoinsLowPopMultiplierStrength = value, true);
            Subs.CVar(_cfg, GoobCVars.GoobcoinUseShortRoundPenalty, value => _goobcoinsUseShortRoundPenalty = value, true);
            Subs.CVar(_cfg, GoobCVars.GoobcoinShortRoundPenaltyTargetMinutes, value => _goobcoinsShortRoundPenaltyTargetMinutes = value, true);
            Subs.CVar(_cfg, GoobCVars.TestcoinsPerPlayer, value => _testcoinsPerPlayer = value, true);
            Subs.CVar(_cfg, GoobCVars.TestcoinNonAntagMultiplier, value => _testcoinsNonAntagMultiplier = value, true);
            Subs.CVar(_cfg, GoobCVars.TestcoinServerMultiplier, value => _testcoinsServerMultiplier = value, true);
            Subs.CVar(_cfg, GoobCVars.TestcoinMinPlayers, value => _testcoinsMinPlayers = value, true);
            Subs.CVar(_cfg, GoobCVars.TestcoinUseLowpopMultiplier, value => _testcoinsUseLowPopMultiplier = value, true);
            Subs.CVar(_cfg, GoobCVars.TestcoinLowpopMultiplierStrength, value => _testcoinsLowPopMultiplierStrength = value, true);
            Subs.CVar(_cfg, GoobCVars.TestcoinUseShortRoundPenalty, value => _testcoinsUseShortRoundPenalty = value, true);
            Subs.CVar(_cfg, GoobCVars.TestcoinShortRoundPenaltyTargetMinutes, value => _testcoinsShortRoundPenaltyTargetMinutes = value, true);
        }

        public override void Shutdown()
        {
            base.Shutdown();
            _currencyMan.BalanceChange -= OnBalanceChange;
        }

        private void OnRoundEndText(RoundEndTextAppendEvent ev)
        {
            if (_players.PlayerCount < _goobcoinsMinPlayers)
                return;
            if (_players.PlayerCount < _testcoinsMinPlayers)
                return;

            var lowPopMultiplier = 1.0 - (_players.PlayerCount / (double)_players.MaxPlayers);

            var query = EntityQueryEnumerator<MindContainerComponent>();

            while (query.MoveNext(out var uid, out var mindContainer))
            {
                var isBorg = HasComp<BorgChassisComponent>(uid);
                if (!(HasComp<HumanoidAppearanceComponent>(uid)
                    || HasComp<BorgBrainComponent>(uid)
                    || isBorg))
                    continue;

                if (mindContainer.Mind.HasValue)
                {
                    var mind = Comp<MindComponent>(mindContainer.Mind.Value);
                    if (mind is not null
                        && (isBorg || !_mind.IsCharacterDeadIc(mind)) // Borgs count always as dead so I'll just throw them a bone and give them an exception.
                        && mind.OriginalOwnerUserId.HasValue
                        && _players.TryGetSessionById(mind.UserId, out var session))
                    {
                        int money = _goobcoinsPerPlayer;
                        if (session is not null)
                        {
                            money += _jobs.GetJobGoobcoins(session);
                            if (!_jobs.CanBeAntag(session))
                                money *= _goobcoinsNonAntagMultiplier;
                        }
                        int testmoney = _testcoinsPerPlayer;
                        if (session is not null)
                        {
                            testmoney += _jobs.GetJobTestcoins(session);
                            if (!_jobs.CanBeAntag(session))
                                testmoney *= _testcoinsNonAntagMultiplier;
                        }

                        if(_goobcoinsUseLowPopMultiplier)
                            testmoney += (int)Math.Round(testmoney * lowPopMultiplier * _goobcoinsLowPopMultiplierStrength);

                        if (_goobcoinsServerMultiplier != 1)
                            testmoney *= _goobcoinsServerMultiplier;

                        if (session != null && _linkAccount.GetPatron(session)?.Tier != null)
                            testmoney *= 2;

                        if (_goobcoinsUseShortRoundPenalty)
                        {
                            var roundMinutesActual = _gameTicker.RoundDuration().TotalMinutes;
                            testmoney = (int) (testmoney * Math.Min(1, roundMinutesActual / _goobcoinsShortRoundPenaltyTargetMinutes));
                        }

                        if(_testcoinsUseLowPopMultiplier)
                            testmoney += (int)Math.Round(testmoney * lowPopMultiplier * _testcoinsLowPopMultiplierStrength);

                        if (_testcoinsServerMultiplier != 1)
                            testmoney *= _testcoinsServerMultiplier;

                        if (session != null && _linkAccount.GetPatron(session)?.Tier != null)
                            testmoney *= 2;

                        if (_testcoinsUseShortRoundPenalty)
                        {
                            var roundMinutesActual = _gameTicker.RoundDuration().TotalMinutes;
                            testmoney = (int) (testmoney * Math.Min(1, roundMinutesActual / _testcoinsShortRoundPenaltyTargetMinutes));
                        }

                        _currencyMan.AddCurrency(mind.OriginalOwnerUserId.Value, testmoney);
                    }
                }
            }
        }

        private void OnBalanceRequest(PlayerBalanceRequestEvent ev, EntitySessionEventArgs eventArgs)
        {
            var senderSession = eventArgs.SenderSession;
            var balance = _currencyMan.GetBalance(senderSession.UserId);
            RaiseNetworkEvent(new PlayerBalanceUpdateEvent(balance, balance), senderSession);

        }

        /// <summary>
        /// Calls event that when a player's balance is updated.
        /// Also handles popups
        /// </summary>
        private void OnBalanceChange(PlayerBalanceChangeEvent ev)
        {
            RaiseNetworkEvent(new PlayerBalanceUpdateEvent(ev.NewBalance, ev.OldBalance), ev.UserSes);

            if (ev.UserSes.AttachedEntity.HasValue)
            {
                var userEnt = ev.UserSes.AttachedEntity.Value;
                if (ev.NewBalance > ev.OldBalance)
                    _popupSystem.PopupEntity("+" + _currencyMan.Stringify(ev.NewBalance - ev.OldBalance), userEnt, userEnt, PopupType.Medium);
                else if (ev.NewBalance < ev.OldBalance)
                    _popupSystem.PopupEntity("-" + _currencyMan.Stringify(ev.OldBalance - ev.NewBalance), userEnt, userEnt, PopupType.MediumCaution);
                // I really wanted to do some fancy shit where we also display a little sprite next to the pop-up, but that gets pretty complex for such a simple interaction, so, you get this.
            }
        }
    }
}
