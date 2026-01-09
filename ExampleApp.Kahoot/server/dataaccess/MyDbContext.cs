using Microsoft.EntityFrameworkCore;

namespace dataaccess;

public partial class MyDbContext : DbContext
{
    public MyDbContext(DbContextOptions<MyDbContext> options) : base(options)
    {
    }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        optionsBuilder.UseQueryTrackingBehavior(QueryTrackingBehavior.NoTracking);
        base.OnConfiguring(optionsBuilder);
    }

    public virtual DbSet<Answer> Answers { get; set; }

    public virtual DbSet<Game> Games { get; set; }

    public virtual DbSet<Gamemember> Gamemembers { get; set; }

    public virtual DbSet<Gameround> Gamerounds { get; set; }

    public virtual DbSet<Option> Options { get; set; }

    public virtual DbSet<Question> Questions { get; set; }

    public virtual DbSet<Quiz> Quizzes { get; set; }

    public virtual DbSet<User> Users { get; set; }

    public virtual DbSet<Userscredential> Userscredentials { get; set; }


    public virtual DbSet<WeatherStation> WeatherStations { get; set; }

    public virtual DbSet<WeatherReading> WeatherReadings { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Answer>(entity =>
        {
            entity.HasKey(e => new { e.Gameround, e.Userid }).HasName("answers_pkey");

            entity.ToTable("answers", "kahoot");

            entity.Property(e => e.Gameround).HasColumnName("gameround");
            entity.Property(e => e.Userid).HasColumnName("userid");
            entity.Property(e => e.Answeredat).HasColumnName("answeredat");
            entity.Property(e => e.Option).HasColumnName("option");

            entity.HasOne(d => d.GameroundNavigation).WithMany(p => p.Answers)
                .HasForeignKey(d => d.Gameround)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("answers_gameround_fkey");

            entity.HasOne(d => d.OptionNavigation).WithMany(p => p.Answers)
                .HasForeignKey(d => d.Option)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("answers_option_fkey");

            entity.HasOne(d => d.User).WithMany(p => p.Answers)
                .HasForeignKey(d => d.Userid)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("answers_userid_fkey");
        });

        modelBuilder.Entity<Game>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("game_pkey");

            entity.ToTable("game", "kahoot");

            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.Hostid).HasColumnName("hostid");
            entity.Property(e => e.Quizid).HasColumnName("quizid");

            entity.HasOne(d => d.Host).WithMany(p => p.Games)
                .HasForeignKey(d => d.Hostid)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("game_hostid_fkey");

            entity.HasOne(d => d.Quiz).WithMany(p => p.Games)
                .HasForeignKey(d => d.Quizid)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("game_quizid_fkey");
        });

        modelBuilder.Entity<Gamemember>(entity =>
        {
            entity.HasKey(e => new { e.Userid, e.Gameid }).HasName("gamemember_pkey");

            entity.ToTable("gamemember", "kahoot");

            entity.Property(e => e.Userid).HasColumnName("userid");
            entity.Property(e => e.Gameid).HasColumnName("gameid");
            entity.Property(e => e.Joinedat).HasColumnName("joinedat");

            entity.HasOne(d => d.Game).WithMany(p => p.Gamemembers)
                .HasForeignKey(d => d.Gameid)
                .HasConstraintName("gamemember_gameid_fkey");

            entity.HasOne(d => d.User).WithMany(p => p.Gamemembers)
                .HasForeignKey(d => d.Userid)
                .HasConstraintName("gamemember_userid_fkey");
        });

        modelBuilder.Entity<Gameround>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("gameround_pkey");

            entity.ToTable("gameround", "kahoot");

            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.Endedat).HasColumnName("endedat");
            entity.Property(e => e.Gameid).HasColumnName("gameid");
            entity.Property(e => e.Questionid).HasColumnName("questionid");
            entity.Property(e => e.Startedat).HasColumnName("startedat");

            entity.HasOne(d => d.Game).WithMany(p => p.Gamerounds)
                .HasForeignKey(d => d.Gameid)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("gameround_gameid_fkey");

            entity.HasOne(d => d.Question).WithMany(p => p.Gamerounds)
                .HasForeignKey(d => d.Questionid)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("gameround_questionid_fkey");
        });

        modelBuilder.Entity<Option>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("options_pkey");

            entity.ToTable("options", "kahoot");

            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.Description).HasColumnName("description");
            entity.Property(e => e.Iscorrect).HasColumnName("iscorrect");
            entity.Property(e => e.Questionid).HasColumnName("questionid");

            entity.HasOne(d => d.Question).WithMany(p => p.Options)
                .HasForeignKey(d => d.Questionid)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("options_questionid_fkey");
        });

        modelBuilder.Entity<Question>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("questions_pkey");

            entity.ToTable("questions", "kahoot");

            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.Description).HasColumnName("description");
            entity.Property(e => e.Quizid).HasColumnName("quizid");
            entity.Property(e => e.Seconds).HasColumnName("seconds");

            entity.HasOne(d => d.Quiz).WithMany(p => p.Questions)
                .HasForeignKey(d => d.Quizid)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("questions_quizid_fkey");
        });

        modelBuilder.Entity<Quiz>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("quizzes_pkey");

            entity.ToTable("quizzes", "kahoot");

            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.Createdby).HasColumnName("createdby");
            entity.Property(e => e.Name).HasColumnName("name");

            entity.HasOne(d => d.CreatedbyNavigation).WithMany(p => p.Quizzes)
                .HasForeignKey(d => d.Createdby)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("quizzes_createdby_fkey");
        });

        modelBuilder.Entity<User>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("users_pkey");

            entity.ToTable("users", "kahoot");

            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.Name).HasColumnName("name");
        });

        modelBuilder.Entity<Userscredential>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("userscredentials_pkey");

            entity.ToTable("userscredentials", "kahoot");

            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.Passwordhash).HasColumnName("passwordhash");
            entity.Property(e => e.Salt).HasColumnName("salt");

            entity.HasOne(d => d.IdNavigation).WithOne(p => p.Userscredential)
                .HasForeignKey<Userscredential>(d => d.Id)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("userscredentials_id_fkey");
        });

      

        modelBuilder.Entity<WeatherStation>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("weatherstations_pkey");

            entity.ToTable("weatherstations", "kahoot");

            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.Name).HasColumnName("name");
        });

        modelBuilder.Entity<WeatherReading>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("weatherreadings_pkey");

            entity.ToTable("weatherreadings", "kahoot");

            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.Stationid).HasColumnName("stationid");
            entity.Property(e => e.Temperature)
                .HasColumnType("numeric(5,2)")
                .HasColumnName("temperature");
            entity.Property(e => e.Humidity)
                .HasColumnType("numeric(5,2)")
                .HasColumnName("humidity");
            entity.Property(e => e.Pressure)
                .HasColumnType("numeric(6,2)")
                .HasColumnName("pressure");
            entity.Property(e => e.Timestamp).HasColumnName("timestamp");

            entity.HasOne(d => d.Station).WithMany(p => p.WeatherReadings)
                .HasForeignKey(d => d.Stationid)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("weatherreadings_stationid_fkey");

            entity.HasIndex(e => e.Stationid);
            entity.HasIndex(e => e.Timestamp);
        });

        OnModelCreatingPartial(modelBuilder);
    }

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
}