using System.Diagnostics.Contracts;

var random = new Random(0);
var game = new Game(4, 350);
for (;;) {
    var round = new Round(game.PlayerCount, random);
    while (round.Winner is null) {
        round.SortCards();
        Console.WriteLine(round.Board);
        Console.WriteLine(
            $"Player {round.Turn} turn, playables: {string.Join(",", round.Playables())}, cards: {string.Join(",", round.CurrentPlayer.Hand)}");
        string line = Console.ReadLine(), err;
        if (line is null or "exit" or "quit") break;
        if (line == "") {
            var playables = round.Playables();
            if (playables.Any(card => card != round.Boner)) {
                Card best = round.BestPlays(playables).First();
                Console.WriteLine($"Player {round.Turn} plays {best}");
                line = best.ToString();
            } else if (playables.Any()) {
                Card best = round.BestBoner(round.CurrentPlayer.Hand.Where(card => !card.IsJoker).ToList());
                Console.WriteLine($"Player {round.Turn} plays Jo {best}");
                line = "Jo " + best;
            } else {
                line = "pass";
            }
        }
        if (line == "pass") {
            int nextPlayer = (round.Turn + 1) % round.Players.Length;
            for (;;) {
                Console.WriteLine(
                    $"Player {nextPlayer} to pass, cards: {string.Join(",", round.Players[nextPlayer].Hand)}");
                string pass = Console.ReadLine();
                if (pass == "") {
                    Card worst = round.WorstCard(round.Players[nextPlayer].Hand);
                    Console.WriteLine($"Player {nextPlayer} passes {worst}");
                    pass = worst.ToString();
                }
                err = round.Pass(new Card(pass));
                if (err is null) break;
                Console.WriteLine(err);
            }
        } else
            err = line.StartsWith("Jo ") ? round.Play(Card.Joker, new Card(line[3..])) : round.Play(new Card(line));
        if (err is not null) Console.WriteLine(err);
    }
    int playerIndex = 0;
    foreach (var player in round.Players) {
        int score = player.Score(round.Boner);
        game.Scores[playerIndex] += score;
        if (score == 0)
            Console.WriteLine($"Player {playerIndex} Wins!");
        else {
            string extra = player.Hand.Any(c => c == round.Boner) ? " Boned!" : "";
            Console.WriteLine($"Player {playerIndex} {string.Join(",", player.Hand)} score: {score}{extra}");
        }
        playerIndex++;
    }
    Console.WriteLine($"Scores: {string.Join(",", game.Scores)}");
    if (game.IsOver) break;
}

public record Game(int PlayerCount, int MaxScore) {
    public bool IsOver => Scores.Any(s => s >= MaxScore);
    public int[] Scores { get; } = new int[PlayerCount];
}

public record Round {
    private readonly Rank[] max = new Rank[4], min = new Rank[4];

    public Round(int players, Random random) {
        var deck = new Deck().Shuffle(random);
        Players = new Player[players];
        int cardsPerPlayer = deck.Count / players, remainderCards = deck.Count % players;
        var startCard = Card.Start;
        for (int i = 0; i < players; i++) {
            Players[i] = new Player(deck, cardsPerPlayer + (i < remainderCards ? 1 : 0));
            if (Players[i].Hand.Any(card => card == startCard)) Turn = i;
        }
        Contract.Assert(deck.Count == 0);
        for (int i = 0; i < 4; i++)
            min[i] = max[i] = Rank.Joker;
    }

    public string Board {
        get {
            string s = "";
            for (int i = 0; i < 4; i++) {
                Suit suit = (Suit)i;
                s += $"{suit.ToChar()}:";
                if (min[i] == Rank.Joker)
                    s += "- ";
                else if (min[i] == max[i])
                    s += $"{min[i].ToChar()} ";
                else
                    s += $"{min[i].ToChar()}-{max[i].ToChar()} ";
            }
            return s + $"Boner:{Boner}";
        }
    }

    public int Turn { get; private set; }
    public Card Boner { get; private set; } = Card.Joker;
    public Player[] Players { get; }
    public Player CurrentPlayer => Players[Turn];

    public Player Winner => Players.FirstOrDefault(p => p.Hand.Count == 0);

    public List<Card> Playables() {
        var hand = Players[Turn].Hand;
        Rank minSpade = min[(int)Suit.Spades], maxSpade = max[(int)Suit.Spades];
        if (maxSpade == Rank.Joker)
            return [hand.Single(card => card == Card.Start), ..hand.Where(card => card.IsJoker)];
        return hand.Where(card => card.Rank is Rank.Seven or Rank.Joker ||
                           max[(int)card.Suit] != Rank.Joker &&
                           (max[(int)card.Suit] == card.Rank - 1 || min[(int)card.Suit] == card.Rank + 1) &&
                           (card.Suit == Suit.Spades || card.Rank >= minSpade && card.Rank <= maxSpade))
            .Where(card => card != Boner || Boner.Rank == Rank.Joker).ToList();
    }

    public string Play(Card card, Card jokerPlayAs = default) {
        if (Players[Turn].Hand.All(c => c != card)) return "You don't have that card";
        Rank minSpade = min[(int)Suit.Spades], maxSpade = max[(int)Suit.Spades];
        bool wasJoker = card.Rank == Rank.Joker;
        if (wasJoker) card = jokerPlayAs;
        if (minSpade == Rank.Joker)
            if (card.Rank != Rank.Seven || card.Suit != Suit.Spades) return "You must play the 7 of Spades";
        if (card.Rank == Rank.Seven && min[(int)card.Suit] == Rank.Joker)
            min[(int)card.Suit] = max[(int)card.Suit] = card.Rank;
        else if (card.Rank == min[(int)card.Suit] - 1) {
            if (card.Suit != Suit.Spades && card.Rank < minSpade)
                return "Hasn't been broken yet";
            min[(int)card.Suit] = card.Rank;
        } else if (card.Rank == max[(int)card.Suit] + 1) {
            if (card.Suit != Suit.Spades && card.Rank > maxSpade)
                return "Hasn't been broken yet";
            max[(int)card.Suit] = card.Rank;
        } else
            return "You can't play that card";
        Players[Turn].Use(wasJoker ? Boner : card);
        if (wasJoker) Boner = jokerPlayAs;
        NextTurn();
        return null;
    }

    public string Pass(Card card) {
        int nextPlayer = (Turn + 1) % Players.Length;
        if (Players[nextPlayer].Hand.All(c => c != card)) return "You don't have that card";
        Players[Turn].Hand.Add(card);
        NextTurn();
        Players[Turn].Use(card);
        return null;
    }

    private void NextTurn() => Turn = ++Turn % Players.Length;

    public void SortCards() => Players[Turn].SortHand(Boner);

    public List<Card> BestPlays(List<Card> playables) => playables.Where(card => card != Boner).OrderBy(OpenSpots).ToList();

    public int OpenSpots(Card afterPlay) {
        Rank[] localMin = (Rank[])min.Clone(), localMax = (Rank[])max.Clone();
        if (!afterPlay.IsJoker) {
            if (afterPlay.Rank < localMin[(int)afterPlay.Suit]) localMin[(int)afterPlay.Suit] = afterPlay.Rank;
            if (afterPlay.Rank > localMax[(int)afterPlay.Suit]) localMax[(int)afterPlay.Suit] = afterPlay.Rank;
        }
        Rank minSpade = localMin[(int)Suit.Spades], maxSpade = localMax[(int)Suit.Spades];
        int spots = 0;
        if (minSpade > Rank.Ace) spots++;
        if (maxSpade < Rank.King) spots++;
        foreach (var suit in Enum.GetValues<Suit>().Where(suit => suit is not Suit.Spades and not Suit.None)) {
            if (localMin[(int)suit] > minSpade) spots++;
            if (localMax[(int)suit] < maxSpade) spots++;
        }
        return spots;
    }

    public Card BestBoner(List<Card> hand) {
        Card best = Card.Joker;
        int bestScore = int.MinValue; // number of blocking cards
        Rank minSpade = min[(int)Suit.Spades], maxSpade = max[(int)Suit.Spades];
        foreach (Suit suit in Enum.GetValues<Suit>().Where(suit => suit != Suit.None)) {
            Rank lo = min[(int)suit], hi = max[(int)suit];
            if (lo == Rank.Joker || hi == Rank.Joker) {
                lo = Rank.Eight;
                hi = Rank.Six;
                // will make loCard == hiCard (7 of suit)
            }
            Card loCard = new(suit, lo - 1), hiCard = new(suit, hi + 1);
            int score = 0;
            if (suit == Suit.Spades || lo - 1 >= minSpade) {
                int add = 32;
                if (hand.Contains(loCard)) score -= 100; // avoid boning ourselves
                for (lo -= 2; lo >= Rank.Ace && add > 0; lo--) {
                    if (suit != Suit.Spades && lo < minSpade) add >>= 1;
                    if (hand.Contains(new Card(suit, lo))) score += add;
                    else add >>= 1;
                }
                if (loCard != hiCard) {
                    if (score > bestScore) {
                        bestScore = score;
                        best = loCard;
                    }
                
                    score = 0;
                    if (hand.Contains(hiCard)) score -= 100; // avoid boning ourselves
                }
            }
            if (suit == Suit.Spades || hi + 1 <= maxSpade) {
                int add = 32;
                for (hi += 2; hi <= Rank.King && add > 0; hi++) {
                    if (suit != Suit.Spades && hi > maxSpade) add >>= 1;
                    if (hand.Contains(new Card(suit, hi)))
                        score += hi >= Rank.Ten ? add * 3 / 2 : add; // +50% for 10pt cards
                    else add >>= 1;
                }
                if (score > bestScore) {
                    bestScore = score;
                    best = hiCard;
                }
            }
        }
        return best;
    }

    public int BlockingScore(Card card) {
        Rank spadesMin = min[(int)Suit.Spades], spadesMax = max[(int)Suit.Spades];
        int score = 0;
        if (card.Suit == Suit.Spades) {
            if (card.Rank < spadesMin) {
                score = spadesMin - card.Rank - 1;
            } else {
                score = card.Rank - spadesMax - 1;
            }
        } else {
            if (card.Rank < spadesMin) {
                score = (spadesMin - card.Rank) * 2; // bonus for having spades blocking too
            } else if (card.Rank > spadesMax) {
                score = (spadesMax - card.Rank) * 2; // bonus for having spades blocking too
            }
            if (card.Rank < min[(int)card.Suit]) {
                score += min[(int)card.Suit] - card.Rank - 1;
            } else {
                score += card.Rank - max[(int)card.Suit] - 1;
            }
            score *= 2; // bonus for not being spades
        }
        return score;
    }

    public Card WorstCard(List<Card> hand) {
        if (hand.Count == 1) return hand.First();
        int highestScore = int.MinValue;
        Card worst = default;
        foreach (Card card in hand.Where(card => !card.IsJoker)) {
            if (card == Boner) return card;
            int score = BlockingScore(card);
            if (card.Rank >= Rank.Ten) score++; // even worse if it could add 10pts instead of 5
            if (score > highestScore) {
                highestScore = score;
                worst = card;
            }
        }
        return worst;
    }
}

public readonly record struct Deck() {
    private readonly List<Card> cards = Enum.GetValues<Suit>()
        .Where(suit => suit != Suit.None).SelectMany(suit => Enum.GetValues<Rank>()
            .Where(rank => rank != Rank.Joker).Select(rank => new Card(suit, rank)))
        .Concat([Card.Joker]).ToList();

    public int Count => cards.Count;

    public Deck Shuffle(Random random) { // Fisher-Yates shuffle
        for (int i = cards.Count - 1; i > 0; i--) {
            int j = random.Next(i + 1);
            (cards[i], cards[j]) = (cards[j], cards[i]);
        }
        return this;
    }

    public Card Draw() {
        Card card = cards[^1];
        cards.RemoveAt(cards.Count - 1);
        return card;
    }
}

public readonly record struct Card(Suit Suit, Rank Rank) {
    public static Card Joker { get; } = new(Suit.None, Rank.Joker);
    public static Card Start { get; } = new(Suit.Spades, Rank.Seven);
    
    public Card(string str) : this(Suit.None, Rank.Joker) {
        if (str != "Jo") {
            char rank = str[0], suit = char.ToUpper(str[1]);
            Rank = char.IsDigit(rank)
                ? (Rank)(rank - '0')
                : Enum.GetValues<Rank>().Last(r => r != Rank.Joker && r.ToString()[0] == rank);
            Suit = Enum.GetValues<Suit>().First(s => s != Suit.None && s.ToString()[0] == suit);
        }
    }

    public int Value => Rank >= Rank.Ten ? 10 : 5;

    public bool IsJoker => Rank is Rank.Joker;

    public override string ToString() => $"{Rank.ToChar()}{Suit.ToChar()}";
}

public enum Rank {
    Joker = 0,
    Ace,
    Two,
    Three,
    Four,
    Five,
    Six,
    Seven,
    Eight,
    Nine,
    Ten,
    Jack,
    Queen,
    King
}

public enum Suit {
    None = -1, // only used for Joker
    Spades,
    Hearts,
    Clubs,
    Diamonds,
}

public class Player(Deck deck, int cards) {
    public readonly List<Card> Hand = Enumerable.Range(0, cards).Select(_ => deck.Draw()).ToList();

    public void Use(Card card) {
        if (Hand.RemoveAll(c => c == card) != 1)
            throw new ArgumentException($"Card {card} not found in hand");
    }
    
    public void SortHand(Card boner) {
        Hand.Sort((a, b) => a == boner ? -1 : b == boner ? 1 : a.Suit != b.Suit ? a.Suit - b.Suit : a.Rank - b.Rank);
    }

    public int Score(Card boner) => Hand.Sum(card => card == boner ? 50 : card.Value);
}

public static class Extensions {
    public static char ToChar(this Rank rank) =>
        rank is >= Rank.Two and <= Rank.Nine ? (char)('0' + (int)rank) : rank.ToString()[0];

    public static char ToChar(this Suit suit) => suit is Suit.None ? 'o' : char.ToLower(suit.ToString()[0]);
}