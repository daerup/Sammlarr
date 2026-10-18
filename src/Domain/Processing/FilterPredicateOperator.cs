namespace Domain.Processing;

public enum FilterPredicateOperator
{
	Equals,
	NotEquals,
	Contains,
	NotContains,
	StartsWith,
	EndsWith,
	IsEmpty,
	IsNotEmpty,
	GreaterThan,
	LessThan,
	GreaterOrEqual,
	LessOrEqual,
	Between,
	IsTrue,
	IsFalse,
	In,
	NotIn,
	ContainsAll,
}
