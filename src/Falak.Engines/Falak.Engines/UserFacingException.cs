using System;

namespace Falak.Engines;

public sealed class UserFacingException : Exception
{
	public UserFacingException(string message, Exception? inner = null)
		: base(message, inner)
	{
	}
}
