namespace WebFormsPageless
{
    // Data model. Private snake_case fields bind to SQLite columns;
    // public PascalCase properties for C# access.
    // Pageless uses only the core fields, but the full schema columns
    // are present so the class maps cleanly to the shared table.
    public class obBook
    {
        int id = 0;
        string title = "";
        string author = "";
        string genre = "";
        int year = 0;
        string format = "";
        string tags = "";
        string availability = "";
        int in_stock = 0;
        string date_created = "";

        public int Id { get { return id; } set { id = value; } }
        public string Title { get { return title; } set { title = value; } }
        public string Author { get { return author; } set { author = value; } }
        public string Genre { get { return genre; } set { genre = value; } }
        public int Year { get { return year; } set { year = value; } }
        public string Format { get { return format; } set { format = value; } }
        public string Tags { get { return tags; } set { tags = value; } }
        public string Availability { get { return availability; } set { availability = value; } }
        public int InStock { get { return in_stock; } set { in_stock = value; } }
        public string DateCreated { get { return date_created; } set { date_created = value; } }
    }
}
