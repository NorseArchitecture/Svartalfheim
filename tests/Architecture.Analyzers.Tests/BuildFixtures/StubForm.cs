using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;

namespace Fixture;

public abstract class StubForm : ComponentBase
{
	protected EditContext EditContextFor(object request) =>
		new(request);
}
