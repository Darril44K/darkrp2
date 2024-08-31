namespace Dxura.Darkrp;

public static class IEnumerableExtensions
{
	public static int GetHashCode<T>( this IEnumerable<T> self, Func<T, int> hashcode )
	{
		var asHashCodes = self.Select( x => hashcode( x ) );

		var calculatedHash = 0;
		foreach ( var hash in asHashCodes )
		{
			calculatedHash += hash;
		}

		return calculatedHash;
	}
}
