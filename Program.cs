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
        if (line == "pass") {
            int nextPlayer = (round.Turn + 1) % round.Players.Length;
            for (;;) {
                Console.WriteLine(
                    $"Player {nextPlayer} to pass, cards: {string.Join(",", round.Players[nextPlayer].Hand)}");
                string pass = Console.ReadLine();
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