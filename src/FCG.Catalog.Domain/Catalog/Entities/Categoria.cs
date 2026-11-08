namespace FCG.Catalog.Domain.Catalog.Entities;

public class Categoria
{
    // 1 - Informando parämetros de construtor para o EF Core
    public Guid Id { get; private set; }

    public string Nome { get; private set; } = string.Empty;

    // 2 - Construtor privado para o EF Core
    private Categoria()
    {
        
    }

    // 3 - Construtor público para criar uma nova categoria
    public Categoria(Guid id, string nome)
    {
       if (id == Guid.Empty)
            throw new ArgumentException("O identificador da categoria não pode ser vazio.", nameof(id));

        Id = id;
        AlterarNome(nome);
    }

    // 4. Método para alterar o nome da categoria
    public void AlterarNome(string nome)
    {
        // Validação do nome da categoria
        ArgumentException.ThrowIfNullOrWhiteSpace(nome, nameof(nome));

        // Removendo espaços em branco no início e no final do nome
        var nomeFormatado = nome.Trim();

        // Validação do tamanho do nome da categoria
        if (nomeFormatado.Length > 200)
            throw new ArgumentException(
                $"O nome da categoria não pode ter mais de 200 caracteres. Valor informado: '{nomeFormatado}'", nameof(nome));

        Nome = nomeFormatado;    
    }

}
