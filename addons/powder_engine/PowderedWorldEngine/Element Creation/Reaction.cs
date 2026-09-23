using Godot;
using static Elements;

[Tool]
[GlobalClass]
public partial class Reaction : Resource
	{

		public enum Comparisons
		{
			EQUALS,
			GREATER_THAN_OR_EQUAL,
			LESS_THAN
		}

		public enum ReactionTypes
		{
			REPLACE_SELF,
			REPLACE_NEIGHBORS,
			REPLACE_MARKED_NEIGHBORS,
			REPLACE_LAST_ITERATED_NEIGHBOR,
		}

		/// <summary>
		/// The neighbors the reaction checks to find if the it meets the criteria to cause the reaction.
		/// </summary>
		[Export] public Godot.Collections.Array<Vector2I> MarkedNeighbors;

		[Export] public Godot.Collections.Array<AllElements> ElementExclusions;

        private AllElements? _elementCheck;

		/// <summary>
		/// The element to check MarkedNeighbors for; when this value is <c>null</c>, it means ANY
		/// </summary>
	    public AllElements? ElementCheck
    {
        get => _elementCheck;
        set => _elementCheck = value;
    }

       [Export] public Variant ElementCheckSerialized
    {
        get
        {
            if(_elementCheck == null)
            {
                return default;
            }
            else
            {
                return (int)_elementCheck.Value;
            }
        }
        set
        {
            if(value.VariantType == Variant.Type.Nil)
            {
                _elementCheck = null;
            }
            else
            {
                _elementCheck = (AllElements)value.AsInt32();
            }
        }
    }

		[Export] public Comparisons NeighborElementComparison;
		[Export] public ReactionTypes reactionType;
		/// <summary>
		/// The element to replace the other cells with if the ReactionType is a Replace type.
		/// </summary>
		[Export] public AllElements ReplaceWith;
		[Export] public int MatchingNumToCheck;

		public Reaction(Godot.Collections.Array<Vector2I> markedNeighbors, AllElements? ElementToCheck, Comparisons NeighborComparison, ReactionTypes ReactionType, AllElements ReplaceElement, int matchingNumToCheck, Godot.Collections.Array<AllElements> elementExclusions = null)
		{
			MarkedNeighbors = markedNeighbors;
			ElementCheck = ElementToCheck;
			NeighborElementComparison = NeighborComparison;
			reactionType = ReactionType;
			ReplaceWith = ReplaceElement;
			MatchingNumToCheck = matchingNumToCheck;

			if(elementExclusions == null)
			{
				ElementExclusions = new Godot.Collections.Array<AllElements>{};
			}
			else
			{
				ElementExclusions = elementExclusions;
			}

		}

        public Reaction()
		{
			MarkedNeighbors = new Godot.Collections.Array<Vector2I>{};
			ElementCheck = null;
			NeighborElementComparison = Comparisons.GREATER_THAN_OR_EQUAL;
			reactionType = ReactionTypes.REPLACE_SELF;
			ReplaceWith = AllElements.AIR;
			MatchingNumToCheck = 1;

		}


		public Reaction Clone()
		{
			return new Reaction(MarkedNeighbors, ElementCheck, NeighborElementComparison, reactionType, ReplaceWith, MatchingNumToCheck, ElementExclusions);
		}

	}
