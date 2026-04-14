using Pdmt.Maui.ViewModels.Cards;

namespace Pdmt.Maui.Views;

public class InsightCardTemplateSelector : DataTemplateSelector
{
    public DataTemplate? Card01Template { get; set; }
    public DataTemplate? Card02Template { get; set; }
    public DataTemplate? Card03Template { get; set; }
    public DataTemplate? Card04Template { get; set; }
    public DataTemplate? Card05Template { get; set; }
    public DataTemplate? Card06Template { get; set; }
    public DataTemplate? Card07Template { get; set; }
    public DataTemplate? Card08Template { get; set; }
    public DataTemplate? Card09Template { get; set; }
    public DataTemplate? Card10Template { get; set; }

    protected override DataTemplate? OnSelectTemplate(object item, BindableObject container) =>
        item switch
        {
            Card01IntenseTagsViewModel  => Card01Template,
            Card02RepeatingViewModel    => Card02Template,
            Card03BalanceViewModel      => Card03Template,
            Card04TrendViewModel        => Card04Template,
            Card05DiscountedPosViewModel => Card05Template,
            Card06WeekdaysViewModel     => Card06Template,
            Card07NextDayViewModel      => Card07Template,
            Card08TagCombosViewModel    => Card08Template,
            Card09TagTrendViewModel     => Card09Template,
            Card10InfluenceViewModel    => Card10Template,
            _                           => null
        };
}
