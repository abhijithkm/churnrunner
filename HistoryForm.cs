namespace ChurnRunner;

/// <summary>A simple list of the last runs read from history.json.</summary>
internal sealed class HistoryForm : Form
{
    public HistoryForm(IReadOnlyList<RunRecord> runs)
    {
        Text = "Run history";
        Font = Theme.Body();
        BackColor = Theme.Canvas;
        ClientSize = new Size(900, 460);
        MinimumSize = new Size(700, 320);
        StartPosition = FormStartPosition.CenterParent;
        ShowInTaskbar = false;

        var list = new ListView
        {
            Dock = DockStyle.Fill,
            View = View.Details,
            FullRowSelect = true,
            GridLines = false,
            BorderStyle = BorderStyle.None,
            HeaderStyle = ColumnHeaderStyle.Nonclickable,
            Font = Theme.Body(9.5F)
        };
        list.Columns.Add("Started", 150);
        list.Columns.Add("Took", 70, HorizontalAlignment.Right);
        list.Columns.Add("Started by", 110);
        list.Columns.Add("ASC / DESC", 100, HorizontalAlignment.Right);
        list.Columns.Add("Result", 80);
        list.Columns.Add("Left after run", 110, HorizontalAlignment.Right);
        list.Columns.Add("Duplicate rows", 110, HorizontalAlignment.Right);

        foreach (var r in runs)
        {
            var took = r.Finished - r.Started;
            var item = new ListViewItem(r.Started.ToString("ddd d MMM yyyy  HH:mm"));
            item.SubItems.Add($"{(int)took.TotalMinutes}:{took.Seconds:00}");
            item.SubItems.Add(r.Trigger);
            item.SubItems.Add($"{r.HalfAsc} / {r.HalfDesc}");
            item.SubItems.Add(r.Result);
            item.SubItems.Add(r.PendingAfter?.ToString() ?? "–");
            item.SubItems.Add(r.DuplicateLocations?.ToString() ?? "–");
            item.UseItemStyleForSubItems = false;
            item.SubItems[4].ForeColor = r.Result switch { "Done" => Theme.Good, "Failed" => Theme.Fault, _ => Theme.Slate };
            if (r.PendingAfter > 0) item.SubItems[5].ForeColor = Theme.Desc;
            if (r.DuplicateLocations > 0) item.SubItems[6].ForeColor = Theme.Fault;
            list.Items.Add(item);
        }

        var card = new Card { Dock = DockStyle.Fill, Padding = new Padding(14) };
        if (runs.Count == 0)
        {
            card.Controls.Add(new Label
            {
                Text = "No runs recorded yet. Each run is added here when it finishes.",
                ForeColor = Theme.Slate,
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleCenter
            });
        }
        else
        {
            card.Controls.Add(list);
        }

        var host = new Panel { Dock = DockStyle.Fill, Padding = new Padding(16), BackColor = Theme.Canvas };
        host.Controls.Add(card);
        Controls.Add(host);
    }
}
