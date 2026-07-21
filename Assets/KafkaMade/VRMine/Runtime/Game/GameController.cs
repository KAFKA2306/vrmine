using UdonSharp;
using UnityEngine;
using VRC.SDKBase;

[UdonBehaviourSyncMode(BehaviourSyncMode.Manual)]
public class GameController : UdonSharpBehaviour
{
    public BoardState board;
    public BoardView view;
    public PlayerClient[] mailboxes = new PlayerClient[0];
    public WaveSimulator wave;
    public LogStream logStream;
    public int localPlayerSeat = -1;
    public byte[] specialTrumpKinds = new byte[] { 2, 1, 3, 5, 6, 4 };
    [UdonSynced] public uint boardSeed;
    [UdonSynced] public uint boardHash;
    [UdonSynced] public int turnIndex;
    [UdonSynced] public int winnerPlayerId;
    [UdonSynced] public byte declarationResult;
    [UdonSynced] public byte logHead;
    [UdonSynced] public byte logCount;
    [UdonSynced] public byte[] logData = new byte[NetConst.LogRingSize * 4];
    int[] handledSequence = new int[64];
    uint randomState;

    void Start()
    {
        if (Networking.IsOwner(gameObject) && board.phase == BoardState.PhaseSetup) SetupGame();
        Render();
    }

    public override void OnDeserialization()
    {
        if (!HasLocalSeat()) localPlayerSeat = -1;
        SchedulePendingResolution();
        Render();
    }

    public override void OnOwnershipTransferred(VRCPlayerApi player)
    {
        if (player != null && player.isLocal) SchedulePendingResolution();
    }

    public void ConfigurePlayers(int count)
    {
        OwnState();
        board.playerCount = (byte)Mathf.Clamp(count, 3, NetConst.MaxPlayers);
        localPlayerSeat = -1;
        for (int i = 0; i < board.occupiedPlayerIds.Length; i++) board.occupiedPlayerIds[i] = 0;
        ClearForWaitingRoom();
        Sync();
    }

    public void SetupGame()
    {
        OwnState();
        if (!AllSeatsOccupied())
        {
            ClearForWaitingRoom();
            Sync();
            return;
        }

        boardSeed = (uint)Random.Range(1, int.MaxValue);
        randomState = boardSeed;
        turnIndex = 0;
        winnerPlayerId = 0;
        board.roundIndex = 0;
        board.dealerSeat = 0;
        for (int i = 0; i < NetConst.MaxPlayers; i++) board.scores[i] = 0;
        for (int i = 0; i < 60; i++) board.ruleDeck[i] = (byte)(i + 1);
        Shuffle(board.ruleDeck, 60);
        board.ruleDeckCursor = 0;
        for (int i = 0; i < board.ruleHands.Length; i++) board.ruleHands[i] = 0;
        RefillRules();
        StartRound();
    }

    public void SelectRule(int handIndex)
    {
        if (board.phase != BoardState.PhaseRuleSelect) return;
        int seat = localPlayerSeat;
        if (seat < 0 || seat >= board.playerCount || handIndex < 0 || handIndex >= 3) return;
        if (board.playerCount == 5 && seat == (board.dealerSeat + 1) % board.playerCount) return;
        byte rule = board.ruleHands[seat * 3 + handIndex];
        if (rule == 0 || board.selectedRuleBySeat[seat] != 0) return;
        OwnState();
        board.selectedRuleBySeat[seat] = rule;
        board.ruleHands[seat * 3 + handIndex] = 0;
        if (AllRulesSelected()) ActivateRules();
        Sync();
    }

    public void OnCardClicked(int handIndex)
    {
        if (!HasLocalSeat()) return;
        if (board.phase == BoardState.PhasePrepare)
        {
            ToggleMarkedCard(handIndex);
            return;
        }
        if (board.phase != BoardState.PhasePlayCard || board.currentPlayerSeat != localPlayerSeat) return;
        OwnState();
        TryPlayCard(localPlayerSeat, handIndex);
    }

    public void ConfirmMarkedCards()
    {
        if (!HasLocalSeat() || board.phase != BoardState.PhasePrepare) return;
        int required = board.prepareStep == 3 ? 1 : 3;
        int count = 0;
        int offset = localPlayerSeat * NetConst.MaxHandSize;
        for (int i = 0; i < NetConst.MaxHandSize; i++) count += board.markedCards[offset + i];
        if (count != required) return;
        OwnState();
        board.confirmedMask |= (byte)(1 << localPlayerSeat);
        if (board.confirmedMask == (1 << board.playerCount) - 1) ApplyPreparation();
        Sync();
    }

    public void TryPlayCard(int playerSeat, int handIndex)
    {
        if (playerSeat != board.currentPlayerSeat || handIndex < 0 || handIndex >= NetConst.MaxHandSize) return;
        int offset = playerSeat * NetConst.MaxHandSize;
        byte card = board.playerHands[offset + handIndex];
        if (card == 0 || !LegalCard(playerSeat, card)) return;
        int slot = board.trickCardCount;
        board.trickCards[slot] = card;
        board.trickSeats[slot] = (byte)playerSeat;
        board.trickCardCount++;
        board.playerHands[offset + handIndex] = 0;
        turnIndex++;

        if (board.trickCardCount == board.playerCount)
        {
            board.phase = BoardState.PhaseResolveTrick;
            Sync();
            SendCustomEventDelayedSeconds(nameof(ResolvePendingTrick), HasRule(26) ? 2.25f : 0.55f);
            return;
        }

        board.currentPlayerSeat = (byte)NextSeat(playerSeat, CurrentDirection());
        Sync();
    }

    public void ResolvePendingTrick()
    {
        if (!Networking.IsOwner(gameObject)) return;
        if (board.phase != BoardState.PhaseResolveTrick || board.trickCardCount != board.playerCount) return;
        ResolveTrick();
        Sync();
    }

    public bool ShouldHideTrickCard(int slot)
    {
        return HasRule(26)
            && board.phase == BoardState.PhasePlayCard
            && slot > 0
            && slot < board.trickCardCount;
    }

    public bool RuleActive(int rule)
    {
        return HasRule(rule);
    }

    public void OnDeclare()
    {
        SendCustomNetworkEvent(VRC.Udon.Common.Interfaces.NetworkEventTarget.Owner, nameof(Pull));
    }

    public void Pull()
    {
        if (!Networking.IsOwner(gameObject)) return;
        for (int i = 0; i < mailboxes.Length; i++)
        {
            PlayerClient client = mailboxes[i];
            int slot = client.ownerPlayerId & 63;
            if (handledSequence[slot] == client.requestSequence) continue;
            handledSequence[slot] = client.requestSequence;
        }
    }

    public void JoinGame(int seat)
    {
        VRCPlayerApi player = Networking.LocalPlayer;
        if (player == null || seat < 0 || seat >= board.playerCount) return;
        for (int i = 0; i < board.playerCount; i++)
            if (board.occupiedPlayerIds[i] == player.playerId && i != seat) return;
        if (board.occupiedPlayerIds[seat] != 0 && board.occupiedPlayerIds[seat] != player.playerId) return;

        OwnState();
        localPlayerSeat = seat;
        board.occupiedPlayerIds[seat] = player.playerId;
        if (board.phase == BoardState.PhaseSetup && AllSeatsOccupied()) SetupGame();
        else Sync();
    }

    public void Render()
    {
        if (view != null) view.Render();
    }

    public int VerifyRules()
    {
        int failures = 0;
        board.playerCount = 3;
        randomState = 17u;
        DealCards();
        for (int seat = 0; seat < 3; seat++)
        {
            int count = 0;
            for (int slot = 0; slot < NetConst.MaxHandSize; slot++)
            {
                byte card = board.playerHands[seat * NetConst.MaxHandSize + slot];
                if (card == 0) continue;
                count++;
                int rank = Rank(card);
                if (rank == 1 || rank == 10 || rank == 13) failures++;
            }
            if (count != 16) failures++;
        }
        for (int i = 0; i < board.selectedRules.Length; i++) board.selectedRules[i] = 0;
        for (int i = 0; i < board.playerHands.Length; i++) board.playerHands[i] = 0;
        board.trickCardCount = 1;
        board.trickCards[0] = 5;
        board.playerHands[NetConst.MaxHandSize] = 17;
        board.playerHands[NetConst.MaxHandSize + 1] = 1;
        if (LegalCard(1, 17) || !LegalCard(1, 1)) failures++;
        board.selectedRules[0] = 31;
        if (!Beats(4, 8, 2)) failures++;
        board.selectedRules[0] = 5;
        if (!Beats(38, 15, 15)) failures++;

        board.selectedRules[0] = 26;
        board.phase = BoardState.PhasePlayCard;
        board.trickCardCount = 2;
        if (ShouldHideTrickCard(0) || !ShouldHideTrickCard(1)) failures++;
        board.phase = BoardState.PhaseResolveTrick;
        if (ShouldHideTrickCard(1)) failures++;
        return failures;
    }

    bool AllRulesSelected()
    {
        for (int seat = 0; seat < board.playerCount; seat++)
        {
            if (board.playerCount == 5 && seat == (board.dealerSeat + 1) % board.playerCount) continue;
            if (board.selectedRuleBySeat[seat] == 0) return false;
        }
        return true;
    }

    void ActivateRules()
    {
        int count = 0;
        for (int seat = 0; seat < board.playerCount; seat++)
        {
            byte rule = board.selectedRuleBySeat[seat];
            if (rule != 0) board.selectedRules[count++] = rule;
        }
        if (board.playerCount == 3) board.selectedRules[count++] = DrawRule();
        for (int i = count; i < 4; i++) board.selectedRules[i] = 0;
        for (int i = 0; i < count; i++)
            for (int j = i + 1; j < count; j++)
                if (board.selectedRules[j] < board.selectedRules[i])
                {
                    byte swap = board.selectedRules[i];
                    board.selectedRules[i] = board.selectedRules[j];
                    board.selectedRules[j] = swap;
                }
        board.trumpRule = 0;
        board.basicRule = 0;
        board.scoringRule = 0;
        for (int i = 0; i < 4; i++)
        {
            byte rule = board.selectedRules[i];
            if (rule <= 21 && rule != 0) board.trumpRule = rule;
            else if (rule <= 40 && rule != 0) board.basicRule = rule;
            else if (rule != 0) board.scoringRule = rule;
        }
        StartPreparation();
    }

    void StartPreparation()
    {
        board.prepareStep = 0;
        if (HasRule(22)) board.prepareStep = 1;
        else if (HasRule(23)) board.prepareStep = 2;
        else if (HasRule(24)) board.prepareStep = 3;
        else if (HasRule(25)) board.prepareStep = 4;
        if (board.prepareStep == 0) BeginPlay();
        else
        {
            board.phase = BoardState.PhasePrepare;
            ClearMarks();
        }
    }

    void ApplyPreparation()
    {
        int step = board.prepareStep;
        if (step == 1 || step == 2) PassMarkedCards(step == 1 ? -1 : 1);
        else if (step == 3) ReserveMarkedCards();
        else MarkFaceUpCards();
        board.prepareStep = 0;
        if (step < 2 && HasRule(23)) board.prepareStep = 2;
        else if (step < 3 && HasRule(24)) board.prepareStep = 3;
        else if (step < 4 && HasRule(25)) board.prepareStep = 4;
        if (board.prepareStep == 0) BeginPlay();
        else ClearMarks();
    }

    void BeginPlay()
    {
        board.phase = BoardState.PhasePlayCard;
        board.currentPlayerSeat = (byte)((board.dealerSeat + 1) % board.playerCount);
    }

    void ToggleMarkedCard(int handIndex)
    {
        if (localPlayerSeat < 0 || handIndex < 0 || handIndex >= NetConst.MaxHandSize) return;
        int index = localPlayerSeat * NetConst.MaxHandSize + handIndex;
        if (board.playerHands[index] == 0) return;
        OwnState();
        board.markedCards[index] = board.markedCards[index] == 0 ? (byte)1 : (byte)0;
        Sync();
    }

    void PassMarkedCards(int direction)
    {
        byte[] passed = new byte[NetConst.MaxPlayers * 3];
        for (int seat = 0; seat < board.playerCount; seat++)
        {
            int count = 0;
            int offset = seat * NetConst.MaxHandSize;
            for (int i = 0; i < NetConst.MaxHandSize; i++)
                if (board.markedCards[offset + i] != 0)
                {
                    passed[seat * 3 + count++] = board.playerHands[offset + i];
                    board.playerHands[offset + i] = 0;
                }
        }
        for (int seat = 0; seat < board.playerCount; seat++)
        {
            int target = NextSeat(seat, direction);
            for (int i = 0; i < 3; i++) AddCard(target, passed[seat * 3 + i]);
        }
    }

    void ReserveMarkedCards()
    {
        for (int seat = 0; seat < board.playerCount; seat++)
        {
            int offset = seat * NetConst.MaxHandSize;
            for (int i = 0; i < NetConst.MaxHandSize; i++)
                if (board.markedCards[offset + i] != 0)
                {
                    board.reservedCards[seat] = board.playerHands[offset + i];
                    board.playerHands[offset + i] = 0;
                }
        }
    }

    void MarkFaceUpCards()
    {
        for (int i = 0; i < board.markedCards.Length; i++)
        {
            byte card = board.playerHands[i];
            if (card != 0 && board.markedCards[i] != 0) board.faceUpCards[CardId(card)] = 1;
        }
    }

    bool LegalCard(int seat, byte card)
    {
        if (board.trickCardCount == 0) return true;
        byte reference = HasRule(27) ? board.trickCards[board.trickCardCount - 1] : board.trickCards[0];
        int requiredSuit = Suit(reference);
        bool referenceTrump = IsTrump(reference);
        bool hasRequired = false;
        bool hasTrump = false;
        int offset = seat * NetConst.MaxHandSize;
        for (int i = 0; i < NetConst.MaxHandSize; i++)
        {
            byte held = board.playerHands[offset + i];
            if (held == 0) continue;
            bool trump = IsTrump(held);
            if (referenceTrump ? trump : !trump && Suit(held) == requiredSuit) hasRequired = true;
            if (trump) hasTrump = true;
        }
        bool playedFollows = referenceTrump ? IsTrump(card) : !IsTrump(card) && Suit(card) == requiredSuit;
        if (hasRequired) return playedFollows;
        if (HasRule(28) && hasTrump) return IsTrump(card);
        return true;
    }

    void ResolveTrick()
    {
        int winnerSlot = 0;
        for (int i = 1; i < board.trickCardCount; i++) if (Beats(board.trickCards[i], board.trickCards[winnerSlot], board.trickCards[0])) winnerSlot = i;
        if (HasRule(29) && winnerSlot == 0)
        {
            winnerSlot = 1;
            for (int i = 2; i < board.trickCardCount; i++) if (Beats(board.trickCards[i], board.trickCards[winnerSlot], board.trickCards[0])) winnerSlot = i;
        }
        if (HasRule(30)) winnerSlot = SecondHighestSlot();
        int winner = board.trickSeats[winnerSlot];
        int owner = HasRule(35) ? NextSeat(winner, 1) : winner;
        for (int i = 0; i < board.trickCardCount; i++)
        {
            int id = CardId(board.trickCards[i]);
            board.cardOwners[id] = (byte)owner;
            board.cardTricks[id] = board.trickIndex;
        }
        if (HasRule(34))
        {
            int id = CardId(board.trickCards[0]);
            board.cardOwners[id] = 255;
        }
        board.takenTricks[owner]++;
        board.trickIndex++;
        board.trickCardCount = 0;
        for (int i = 0; i < NetConst.MaxPlayers; i++)
        {
            board.trickCards[i] = 0;
            board.trickSeats[i] = 0;
        }
        if (board.trickIndex == 3 && HasRule(38))
        {
            board.prepareStep = 2;
            board.phase = BoardState.PhasePrepare;
            ClearMarks();
            return;
        }
        if (RoundFinished())
        {
            if (HasRule(39)) TransferLastWinner(winner);
            ScoreRound();
            return;
        }
        int leader = HasRule(36) ? NextSeat(winner, -1) : winner;
        board.currentPlayerSeat = (byte)leader;
        board.phase = BoardState.PhasePlayCard;
    }

    int SecondHighestSlot()
    {
        int highest = 0;
        int second = -1;
        for (int i = 1; i < board.trickCardCount; i++)
        {
            if (Beats(board.trickCards[i], board.trickCards[highest], board.trickCards[0]))
            {
                second = highest;
                highest = i;
            }
            else if (second < 0 || Beats(board.trickCards[i], board.trickCards[second], board.trickCards[0])) second = i;
        }
        return second < 0 ? highest : second;
    }

    bool Beats(byte candidate, byte current, byte lead)
    {
        bool candidateTrump = IsTrump(candidate);
        bool currentTrump = IsTrump(current);
        if (candidateTrump != currentTrump) return HasRule(32) ? !candidateTrump : candidateTrump;
        if (candidateTrump)
        {
            int candidateCount = TrumpMatchCount(candidate);
            int currentCount = TrumpMatchCount(current);
            if (candidateCount != currentCount) return candidateCount > currentCount;
            int candidateRule = LowestTrumpRule(candidate);
            int currentRule = LowestTrumpRule(current);
            if (candidateRule != currentRule) return candidateRule < currentRule;
        }
        else if (!HasRule(33))
        {
            bool candidateLead = Suit(candidate) == Suit(lead);
            bool currentLead = Suit(current) == Suit(lead);
            if (candidateLead != currentLead) return candidateLead;
        }
        int candidateRank = Rank(candidate);
        int currentRank = Rank(current);
        if (candidateRank == currentRank) return true;
        return HasRule(31) ? candidateRank < currentRank : candidateRank > currentRank;
    }

    bool IsTrump(byte card)
    {
        return TrumpMatchCount(card) > 0;
    }

    int TrumpMatchCount(byte card)
    {
        int count = 0;
        for (int i = 0; i < 4; i++) if (TrumpRuleMatches(board.selectedRules[i], card)) count++;
        return count;
    }

    int LowestTrumpRule(byte card)
    {
        for (int i = 0; i < 4; i++) if (TrumpRuleMatches(board.selectedRules[i], card)) return board.selectedRules[i];
        return 255;
    }

    bool TrumpRuleMatches(byte rule, byte card)
    {
        if (rule >= 1 && rule <= 14) return Rank(card) == rule + 1;
        if (rule >= 15 && rule <= 20)
        {
            int kind = specialTrumpKinds[rule - 15];
            if (kind <= 4) return Suit(card) == kind - 1;
            if (kind == 5) return Rank(card) == 1;
            return IsFace(card);
        }
        if (rule == 21 && board.trickIndex >= 1 && board.trickCardCount > 0) return Suit(card) == Suit(board.trickCards[0]);
        return false;
    }

    void ScoreRound()
    {
        int[] roundScores = new int[NetConst.MaxPlayers];
        for (int trick = 0; trick < board.trickIndex; trick++) ScoreTrick(roundScores, trick);
        if (HasRule(40)) for (int seat = 0; seat < board.playerCount; seat++) board.takenTricks[seat] += 3;
        for (int i = 0; i < 4; i++) ApplyScoringRule(roundScores, board.selectedRules[i]);
        for (int seat = 0; seat < board.playerCount; seat++) board.scores[seat] += roundScores[seat];
        board.phase = BoardState.PhaseScore;
        board.roundIndex++;
        if (board.roundIndex >= board.playerCount)
        {
            board.phase = BoardState.PhaseComplete;
            int winner = 0;
            for (int seat = 1; seat < board.playerCount; seat++) if (board.scores[seat] > board.scores[winner]) winner = seat;
            winnerPlayerId = board.occupiedPlayerIds[winner];
            return;
        }
        board.dealerSeat = (byte)((board.dealerSeat + 1) % board.playerCount);
        RefillRules();
        StartRound();
    }

    void ScoreTrick(int[] roundScores, int trick)
    {
        int owner = -1;
        int suits = 0;
        bool face = false;
        bool trump = false;
        int value = 1;
        for (int id = 0; id < 60; id++)
        {
            if (board.cardOwners[id] == 255 || board.cardTricks[id] != trick) continue;
            byte card = CardFromId(id);
            owner = board.cardOwners[id];
            suits |= 1 << Suit(card);
            face |= IsFace(card);
            trump |= IsTrump(card);
        }
        if (owner < 0) return;
        if (HasRule(41))
        {
            value = 0;
            for (int suit = 0; suit < 4; suit++) if ((suits & (1 << suit)) != 0) value++;
        }
        if (HasRule(42) && !face) value *= 2;
        if (HasRule(43) && !trump) value *= 2;
        if (HasRule(44) && trick == 4) value *= 3;
        if (HasRule(45))
        {
            if (trick < 2) value = 0;
            if (trick == board.trickIndex - 1) value *= 3;
        }
        roundScores[owner] += value;
    }

    void ApplyScoringRule(int[] points, byte rule)
    {
        if (rule < 46 || rule > 60) return;
        if (rule <= 53)
        {
            for (int id = 0; id < 60; id++)
            {
                int owner = board.cardOwners[id];
                if (owner == 255) continue;
                byte card = CardFromId(id);
                if (rule == 46 && IsFace(card)) points[owner]++;
                else if (rule >= 47 && rule <= 50 && Suit(card) == rule - 47) points[owner]--;
                else if (rule == 51 && IsTrump(card)) points[owner]--;
                else if (rule == 52 && Rank(card) == 7) points[owner] += 3;
                else if (rule == 53 && Rank(card) == 15) points[owner] -= 3;
            }
            return;
        }
        if (rule == 54)
        {
            for (int seat = 0; seat < board.playerCount; seat++) if (board.takenTricks[seat] == 0) points[seat] += 10;
        }
        else if (rule == 55)
        {
            for (int seat = 0; seat < board.playerCount; seat++) if (board.takenTricks[seat] == 1) points[seat] += 5;
        }
        else if (rule == 56)
        {
            for (int seat = 0; seat < board.playerCount; seat++) if ((board.takenTricks[seat] & 1) == 0) points[seat] *= 2;
        }
        else if (rule == 57)
        {
            int least = 255;
            for (int seat = 0; seat < board.playerCount; seat++) if (board.takenTricks[seat] < least) least = board.takenTricks[seat];
            for (int seat = 0; seat < board.playerCount; seat++) if (board.takenTricks[seat] == least) points[seat] *= 2;
        }
        else if (rule == 58 || rule == 59)
        {
            int[] previous = new int[NetConst.MaxPlayers];
            for (int seat = 0; seat < board.playerCount; seat++) previous[seat] = points[seat];
            int direction = rule == 58 ? -1 : 1;
            for (int seat = 0; seat < board.playerCount; seat++)
            {
                int neighbor = previous[NextSeat(seat, direction)];
                points[seat] += rule == 58 && neighbor < 0 ? -neighbor : neighbor;
            }
        }
        else if (rule == 60)
        {
            for (int seat = 0; seat < board.playerCount; seat++) points[seat] = -points[seat];
        }
    }

    void StartRound()
    {
        DealCards();
        board.phase = BoardState.PhaseRuleSelect;
        board.trickIndex = 0;
        board.trickCardCount = 0;
        board.confirmedMask = 0;
        board.prepareStep = 0;
        for (int i = 0; i < board.selectedRules.Length; i++) board.selectedRules[i] = 0;
        for (int i = 0; i < board.selectedRuleBySeat.Length; i++) board.selectedRuleBySeat[i] = 0;
        for (int i = 0; i < board.takenTricks.Length; i++) board.takenTricks[i] = 0;
        for (int i = 0; i < 60; i++)
        {
            board.cardOwners[i] = 255;
            board.cardTricks[i] = 255;
            board.faceUpCards[i] = 0;
        }
        for (int i = 0; i < board.reservedCards.Length; i++) board.reservedCards[i] = 0;
        Sync();
    }

    void DealCards()
    {
        byte[] deck = new byte[60];
        int count = 0;
        for (int suit = 0; suit < 4; suit++)
            for (int rank = 1; rank <= 15; rank++)
                if (board.playerCount != 3 || rank != 1 && rank != 10 && rank != 13) deck[count++] = (byte)((suit << 4) | rank);
        Shuffle(deck, count);
        for (int i = 0; i < board.playerHands.Length; i++) board.playerHands[i] = 0;
        for (int i = 0; i < count; i++) board.playerHands[(i % board.playerCount) * NetConst.MaxHandSize + i / board.playerCount] = deck[i];
    }

    void RefillRules()
    {
        for (int seat = 0; seat < board.playerCount; seat++)
            for (int slot = 0; slot < 3; slot++)
                if (board.ruleHands[seat * 3 + slot] == 0) board.ruleHands[seat * 3 + slot] = DrawRule();
    }

    byte DrawRule()
    {
        if (board.ruleDeckCursor >= board.ruleDeck.Length)
        {
            board.ruleDeckCursor = 0;
            Shuffle(board.ruleDeck, board.ruleDeck.Length);
        }
        return board.ruleDeck[board.ruleDeckCursor++];
    }

    void Shuffle(byte[] values, int count)
    {
        for (int i = count - 1; i > 0; i--)
        {
            randomState = randomState * 1664525u + 1013904223u;
            uint divisor = (uint)(i + 1);
            int j = (int)(randomState - randomState / divisor * divisor);
            byte swap = values[i];
            values[i] = values[j];
            values[j] = swap;
        }
    }

    bool RoundFinished()
    {
        int remaining = 0;
        for (int i = 0; i < board.playerHands.Length; i++) if (board.playerHands[i] != 0) remaining++;
        return HasRule(40) ? remaining == board.playerCount * 3 : remaining == 0;
    }

    void TransferLastWinner(int winner)
    {
        int target = NextSeat(winner, 1);
        for (int id = 0; id < 60; id++) if (board.cardOwners[id] == winner) board.cardOwners[id] = (byte)target;
        board.takenTricks[target] += board.takenTricks[winner];
        board.takenTricks[winner] = 0;
    }

    void ClearMarks()
    {
        board.confirmedMask = 0;
        for (int i = 0; i < board.markedCards.Length; i++) board.markedCards[i] = 0;
    }

    void AddCard(int seat, byte card)
    {
        if (card == 0) return;
        int offset = seat * NetConst.MaxHandSize;
        for (int i = 0; i < NetConst.MaxHandSize; i++)
            if (board.playerHands[offset + i] == 0)
            {
                board.playerHands[offset + i] = card;
                return;
            }
    }

    int CurrentDirection()
    {
        return HasRule(37) && (board.trickIndex & 1) == 1 ? -1 : 1;
    }

    int NextSeat(int seat, int direction)
    {
        return (seat + direction + board.playerCount) % board.playerCount;
    }

    bool HasRule(int rule)
    {
        for (int i = 0; i < board.selectedRules.Length; i++) if (board.selectedRules[i] == rule) return true;
        return false;
    }

    bool AllSeatsOccupied()
    {
        for (int seat = 0; seat < board.playerCount; seat++) if (board.occupiedPlayerIds[seat] == 0) return false;
        return true;
    }

    bool HasLocalSeat()
    {
        VRCPlayerApi player = Networking.LocalPlayer;
        return player != null
            && localPlayerSeat >= 0
            && localPlayerSeat < board.playerCount
            && board.occupiedPlayerIds[localPlayerSeat] == player.playerId;
    }

    void ClearForWaitingRoom()
    {
        winnerPlayerId = 0;
        turnIndex = 0;
        board.phase = BoardState.PhaseSetup;
        board.roundIndex = 0;
        board.currentPlayerSeat = 0;
        board.trickIndex = 0;
        board.trickCardCount = 0;
        board.confirmedMask = 0;
        board.prepareStep = 0;
        for (int i = 0; i < board.playerHands.Length; i++) board.playerHands[i] = 0;
        for (int i = 0; i < board.ruleHands.Length; i++) board.ruleHands[i] = 0;
        for (int i = 0; i < board.selectedRules.Length; i++) board.selectedRules[i] = 0;
        for (int i = 0; i < board.selectedRuleBySeat.Length; i++) board.selectedRuleBySeat[i] = 0;
        for (int i = 0; i < board.trickCards.Length; i++)
        {
            board.trickCards[i] = 0;
            board.trickSeats[i] = 0;
        }
        for (int i = 0; i < board.markedCards.Length; i++) board.markedCards[i] = 0;
        for (int i = 0; i < board.reservedCards.Length; i++) board.reservedCards[i] = 0;
        for (int i = 0; i < board.takenTricks.Length; i++) board.takenTricks[i] = 0;
        for (int i = 0; i < board.scores.Length; i++) board.scores[i] = 0;
    }

    void SchedulePendingResolution()
    {
        if (!Networking.IsOwner(gameObject)) return;
        if (board.phase != BoardState.PhaseResolveTrick || board.trickCardCount != board.playerCount) return;
        SendCustomEventDelayedSeconds(nameof(ResolvePendingTrick), HasRule(26) ? 2.25f : 0.55f);
    }

    int CardId(byte card)
    {
        return Suit(card) * 15 + Rank(card) - 1;
    }

    byte CardFromId(int id)
    {
        return (byte)(((id / 15) << 4) | (id % 15 + 1));
    }

    int Suit(byte card)
    {
        return card >> 4;
    }

    int Rank(byte card)
    {
        return card & 15;
    }

    bool IsFace(byte card)
    {
        int rank = Rank(card);
        return rank == 1 || rank == 10 || rank == 13;
    }

    void OwnState()
    {
        VRCPlayerApi player = Networking.LocalPlayer;
        if (player == null) return;
        if (!Networking.IsOwner(gameObject)) Networking.SetOwner(player, gameObject);
        if (!Networking.IsOwner(board.gameObject)) Networking.SetOwner(player, board.gameObject);
    }

    void Sync()
    {
        board.syncState = Networking.IsOwner(gameObject) ? (byte)1 : (byte)2;
        board.RequestSerialization();
        RequestSerialization();
        Render();
    }
}
