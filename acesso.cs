using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;


namespace ProjetoAcessoConsole
{

    public class Usuario
    {
        public int Id { get; private set; }
        public string Nome { get; set; }
        private List<Ambiente> ambientes;

        public Usuario(int id, string nome)
        {
            Id = id;
            Nome = nome;
            ambientes = new List<Ambiente>();
        }

        public List<Ambiente> GetAmbientes() => ambientes.ToList();

        public bool ConcederPermissao(Ambiente ambiente)
        {
            if (ambientes.Any(a => a.Id == ambiente.Id)) return false;
            ambientes.Add(ambiente);
            return true;
        }

        public bool RevogarPermissao(Ambiente ambiente)
        {
            var ex = ambientes.FirstOrDefault(a => a.Id == ambiente.Id);
            if (ex != null)
            {
                ambientes.Remove(ex);
                return true;
            }
            return false;
        }

        public List<int> GetAmbientesIds() => ambientes.Select(a => a.Id).ToList();

        public override string ToString()
        {
            string ambs = ambientes.Count == 0 ? "(nenhum)" : string.Join(", ", ambientes.Select(a => a.Nome));
            return $"[{Id}] {Nome} - Permissões: {ambs}";
        }
    }

    public class Ambiente
    {
        public int Id { get; private set; }
        public string Nome { get; set; }
        // logs: fila que guarda até 100 ocorrências
        private Queue<Log> logs;

        public Ambiente(int id, string nome)
        {
            Id = id;
            Nome = nome;
            logs = new Queue<Log>();
        }

        public void RegistrarLog(Log log)
        {
            if (logs.Count >= 100)
            {
                logs.Dequeue(); // descarta o mais antigo
            }
            logs.Enqueue(log);
        }

        public List<Log> GetLogs() => logs.ToList();

        // limiar de logs (para salvar)
        public List<Log> FlushLogsForSave() => logs.ToList();

        // re-popular fila a partir de lista (usado ao carregar)
        public void LoadLogsFromList(IEnumerable<Log> lista)
        {
            logs = new Queue<Log>(lista.TakeLast(100));
        }

        public override string ToString()
        {
            return $"[{Id}] {Nome} - Logs armazenados: {logs.Count}";
        }
    }

    public class Log
    {
        public DateTime DtAcesso { get; set; }
        public int UsuarioId { get; set; }
        public bool TipoAcesso { get; set; }

        public Log() { }

        public Log(DateTime dt, int usuarioId, bool tipo)
        {
            DtAcesso = dt;
            UsuarioId = usuarioId;
            TipoAcesso = tipo;
        }

        public override string ToString()
        {
            string tipo = TipoAcesso ? "Autorizado" : "Negado";
            return $"{DtAcesso:yyyy-MM-dd HH:mm:ss} - UsuarioId: {UsuarioId} - {tipo}";
        }
    }

    public class Cadastro
    {
        public List<Usuario> Usuarios { get; private set; }
        public List<Ambiente> Ambientes { get; private set; }

        private string pastaDados;

        private const string ARQ_USUARIOS = "usuarios.csv";
        private const string ARQ_AMBIENTES = "ambientes.csv";
        private const string ARQ_PERMISSOES = "permissoes.csv";
        private const string ARQ_LOGS = "logs.csv";

        public Cadastro(string pastaDados = "")
        {
            Usuarios = new List<Usuario>();
            Ambientes = new List<Ambiente>();
            this.pastaDados = string.IsNullOrWhiteSpace(pastaDados) ? Directory.GetCurrentDirectory() : pastaDados;
        }

        public void AdicionarUsuario(Usuario usuario)
        {
            if (!Usuarios.Any(u => u.Id == usuario.Id))
                Usuarios.Add(usuario);
        }

        public bool RemoverUsuario(Usuario usuario)
        {
            var u = Usuarios.FirstOrDefault(x => x.Id == usuario.Id);
            if (u == null) return false;
            if (u.GetAmbientes().Count > 0) return false;
            return Usuarios.Remove(u);
        }

        public Usuario PesquisarUsuario(Usuario usuario)
        {
            return Usuarios.FirstOrDefault(x => x.Id == usuario.Id);
        }

        public void AdicionarAmbiente(Ambiente ambiente)
        {
            if (!Ambientes.Any(a => a.Id == ambiente.Id))
                Ambientes.Add(ambiente);
        }

        public bool RemoverAmbiente(Ambiente ambiente)
        {
            var a = Ambientes.FirstOrDefault(x => x.Id == ambiente.Id);
            if (a == null) return false;
            foreach (var u in Usuarios)
            {
                u.RevogarPermissao(a); // revoga se existir; ignora retorno
            }
            return Ambientes.Remove(a);
        }

        public Ambiente PesquisarAmbiente(Ambiente ambiente)
        {
            return Ambientes.FirstOrDefault(x => x.Id == ambiente.Id);
        }

        public void Upload()
        {
            Directory.CreateDirectory(pastaDados);

            var sb = new StringBuilder();
            foreach (var u in Usuarios)
            {
                sb.AppendLine($"{u.Id};{Escape(u.Nome)}");
            }
            File.WriteAllText(Path.Combine(pastaDados, ARQ_USUARIOS), sb.ToString(), Encoding.UTF8);

            sb.Clear();
            foreach (var a in Ambientes)
            {
                sb.AppendLine($"{a.Id};{Escape(a.Nome)}");
            }
            File.WriteAllText(Path.Combine(pastaDados, ARQ_AMBIENTES), sb.ToString(), Encoding.UTF8);

            sb.Clear();
            foreach (var u in Usuarios)
            {
                foreach (var ambId in u.GetAmbientesIds())
                {
                    sb.AppendLine($"{u.Id};{ambId}");
                }
            }
            File.WriteAllText(Path.Combine(pastaDados, ARQ_PERMISSOES), sb.ToString(), Encoding.UTF8);

            sb.Clear();
            foreach (var a in Ambientes)
            {
                var logs = a.FlushLogsForSave(); // lista de logs no ambiente
                foreach (var l in logs)
                {
                    int tipo = l.TipoAcesso ? 1 : 0;
                    sb.AppendLine($"{a.Id};{l.DtAcesso:yyyy-MM-dd HH:mm:ss};{l.UsuarioId};{tipo}");
                }
            }
            File.WriteAllText(Path.Combine(pastaDados, ARQ_LOGS), sb.ToString(), Encoding.UTF8);
        }

        public void Download()
        {
            Usuarios = new List<Usuario>();
            Ambientes = new List<Ambiente>();

            string pathUsuarios = Path.Combine(pastaDados, ARQ_USUARIOS);
            if (File.Exists(pathUsuarios))
            {
                var lines = File.ReadAllLines(pathUsuarios, Encoding.UTF8);
                foreach (var ln in lines)
                {
                    if (string.IsNullOrWhiteSpace(ln)) continue;
                    var parts = ln.Split(';');
                    if (parts.Length >= 2 && int.TryParse(parts[0], out int id))
                    {
                        var nome = Unescape(parts[1]);
                        Usuarios.Add(new Usuario(id, nome));
                    }
                }
            }

            string pathAmbientes = Path.Combine(pastaDados, ARQ_AMBIENTES);
            if (File.Exists(pathAmbientes))
            {
                var lines = File.ReadAllLines(pathAmbientes, Encoding.UTF8);
                foreach (var ln in lines)
                {
                    if (string.IsNullOrWhiteSpace(ln)) continue;
                    var parts = ln.Split(';');
                    if (parts.Length >= 2 && int.TryParse(parts[0], out int id))
                    {
                        var nome = Unescape(parts[1]);
                        Ambientes.Add(new Ambiente(id, nome));
                    }
                }
            }

            string pathPerm = Path.Combine(pastaDados, ARQ_PERMISSOES);
            if (File.Exists(pathPerm))
            {
                var lines = File.ReadAllLines(pathPerm, Encoding.UTF8);
                foreach (var ln in lines)
                {
                    if (string.IsNullOrWhiteSpace(ln)) continue;
                    var parts = ln.Split(';');
                    if (parts.Length >= 2 && int.TryParse(parts[0], out int uId) && int.TryParse(parts[1], out int aId))
                    {
                        var u = Usuarios.FirstOrDefault(x => x.Id == uId);
                        var a = Ambientes.FirstOrDefault(x => x.Id == aId);
                        if (u != null && a != null)
                        {
                            u.ConcederPermissao(a);
                        }
                    }
                }
            }

            string pathLogs = Path.Combine(pastaDados, ARQ_LOGS);
            if (File.Exists(pathLogs))
            {
                var map = new Dictionary<int, List<Log>>();
                var lines = File.ReadAllLines(pathLogs, Encoding.UTF8);
                foreach (var ln in lines)
                {
                    if (string.IsNullOrWhiteSpace(ln)) continue;
                    var parts = ln.Split(';');
                    if (parts.Length >= 4 && int.TryParse(parts[0], out int aId) &&
                        DateTime.TryParseExact(parts[1], "yyyy-MM-dd HH:mm:ss", null, System.Globalization.DateTimeStyles.None, out DateTime dt) &&
                        int.TryParse(parts[2], out int uId) && int.TryParse(parts[3], out int tipo))
                    {
                        var l = new Log(dt, uId, tipo == 1);
                        if (!map.ContainsKey(aId)) map[aId] = new List<Log>();
                        map[aId].Add(l);
                    }
                }
                foreach (var kv in map)
                {
                    var a = Ambientes.FirstOrDefault(x => x.Id == kv.Key);
                    if (a != null)
                    {
                        a.LoadLogsFromList(kv.Value);
                    }
                }
            }
        }

        private string Escape(string s) => s?.Replace(";", "\\;").Replace("\n", "\\n") ?? "";
        private string Unescape(string s) => s?.Replace("\\;", ";").Replace("\\n", "\n") ?? "";

        public bool RegistrarAcesso(int ambienteId, int usuarioId)
        {
            var amb = Ambientes.FirstOrDefault(a => a.Id == ambienteId);
            var usu = Usuarios.FirstOrDefault(u => u.Id == usuarioId);
            if (amb == null || usu == null)
            {
                // se ambiente ou usuario nao existem, registrar como negado (se ambiente existe)
                if (amb != null)
                {
                    amb.RegistrarLog(new Log(DateTime.Now, usuarioId, false));
                }
                return false;
            }

            bool autorizado = usu.GetAmbientesIds().Contains(ambienteId);
            amb.RegistrarLog(new Log(DateTime.Now, usuarioId, autorizado));
            return autorizado;
        }
    }

    class Program
    {
        static void Main(string[] args)
        {
            string pasta = Directory.GetCurrentDirectory();
            var cadastro = new Cadastro(pasta);

            cadastro.Download();

            int nextUsuarioId = cadastro.Usuarios.Count == 0 ? 1 : cadastro.Usuarios.Max(u => u.Id) + 1;
            int nextAmbienteId = cadastro.Ambientes.Count == 0 ? 1 : cadastro.Ambientes.Max(a => a.Id) + 1;

            bool sair = false;
            while (!sair)
            {
                Console.Clear();
                Console.WriteLine("=== PROJETO ACESSO - Console (CSV) ===");
                Console.WriteLine($"Usuarios cadastrados: {cadastro.Usuarios.Count}");
                Console.WriteLine($"Ambientes cadastrados: {cadastro.Ambientes.Count}");
                Console.WriteLine();
                Console.WriteLine("0. Sair");
                Console.WriteLine("1. Cadastrar ambiente");
                Console.WriteLine("2. Consultar ambiente");
                Console.WriteLine("3. Excluir ambiente");
                Console.WriteLine("4. Cadastrar usuario");
                Console.WriteLine("5. Consultar usuario");
                Console.WriteLine("6. Excluir usuario");
                Console.WriteLine("7. Conceder permissao ao usuario");
                Console.WriteLine("8. Revogar permissao do usuario");
                Console.WriteLine("9. Registrar acesso");
                Console.WriteLine("10. Consultar logs de acesso (por ambiente)");
                Console.WriteLine();
                Console.Write("Escolha uma opcao: ");
                string opc = Console.ReadLine();

                switch (opc.Trim())
                {
                    case "0":
                        cadastro.Upload();
                        Console.WriteLine("Dados salvos. Saindo...");
                        sair = true;
                        break;

                    case "1":
                        Console.Write("Nome do ambiente: ");
                        var nomeAmb = Console.ReadLine();
                        var amb = new Ambiente(nextAmbienteId++, nomeAmb);
                        cadastro.AdicionarAmbiente(amb);
                        Console.WriteLine($"Ambiente [{amb.Id}] {amb.Nome} cadastrado.");
                        Pause();
                        break;

                    case "2":
                        Console.Write("Id do ambiente: ");
                        if (!int.TryParse(Console.ReadLine(), out int idCons)) { Console.WriteLine("Id invalido."); Pause(); break; }
                        var ambCons = cadastro.PesquisarAmbiente(new Ambiente(idCons, ""));
                        if (ambCons == null) { Console.WriteLine("Ambiente nao encontrado."); Pause(); break; }
                        Console.WriteLine(ambCons);
                        var logs = ambCons.GetLogs();
                        Console.WriteLine($"Logs (ultimos {logs.Count}):");
                        foreach (var l in logs)
                        {
                            Console.WriteLine("  " + l.ToString());
                        }
                        Pause();
                        break;

                    case "3":
                        Console.Write("Id do ambiente: ");
                        if (!int.TryParse(Console.ReadLine(), out int idRemA)) { Console.WriteLine("Id invalido."); Pause(); break; }
                        var remAmb = cadastro.PesquisarAmbiente(new Ambiente(idRemA, ""));
                        if (remAmb == null) { Console.WriteLine("Ambiente nao encontrado."); Pause(); break; }
                        bool okRemA = cadastro.RemoverAmbiente(remAmb);
                        Console.WriteLine(okRemA ? "Ambiente removido." : "Nao foi possivel remover ambiente.");
                        Pause();
                        break;

                    case "4":
                        Console.Write("Nome do usuario: ");
                        var nomeU = Console.ReadLine();
                        var u = new Usuario(nextUsuarioId++, nomeU);
                        cadastro.AdicionarUsuario(u);
                        Console.WriteLine($"Usuario [{u.Id}] {u.Nome} cadastrado.");
                        Pause();
                        break;

                    case "5":
                        Console.Write("Id do usuario: ");
                        if (!int.TryParse(Console.ReadLine(), out int idConsU)) { Console.WriteLine("Id invalido."); Pause(); break; }
                        var usuCons = cadastro.PesquisarUsuario(new Usuario(idConsU, ""));
                        if (usuCons == null) { Console.WriteLine("Usuario nao encontrado."); Pause(); break; }
                        Console.WriteLine(usuCons);
                        Pause();
                        break;

                    case "6":
                        Console.Write("Id do usuario: ");
                        if (!int.TryParse(Console.ReadLine(), out int idRemU)) { Console.WriteLine("Id invalido."); Pause(); break; }
                        var remUsu = cadastro.PesquisarUsuario(new Usuario(idRemU, ""));
                        if (remUsu == null) { Console.WriteLine("Usuario nao encontrado."); Pause(); break; }
                        bool okRemU = cadastro.RemoverUsuario(remUsu);
                        Console.WriteLine(okRemU ? "Usuario removido." : "Nao foi possivel remover usuario (possivelmente possui permissoes).");
                        Pause();
                        break;

                    case "7":
                        Console.Write("Id do usuario: ");
                        if (!int.TryParse(Console.ReadLine(), out int idU7)) { Console.WriteLine("Id invalido."); Pause(); break; }
                        Console.Write("Id do ambiente: ");
                        if (!int.TryParse(Console.ReadLine(), out int idA7)) { Console.WriteLine("Id invalido."); Pause(); break; }
                        var usu7 = cadastro.PesquisarUsuario(new Usuario(idU7, ""));
                        var amb7 = cadastro.PesquisarAmbiente(new Ambiente(idA7, ""));
                        if (usu7 == null || amb7 == null) { Console.WriteLine("Usuario ou ambiente nao encontrados."); Pause(); break; }
                        bool cres = usu7.ConcederPermissao(amb7);
                        Console.WriteLine(cres ? "Permissao concedida." : "Usuario ja possui permissao para esse ambiente.");
                        Pause();
                        break;

                    case "8":
                        Console.Write("Id do usuario: ");
                        if (!int.TryParse(Console.ReadLine(), out int idU8)) { Console.WriteLine("Id invalido."); Pause(); break; }
                        Console.Write("Id do ambiente: ");
                        if (!int.TryParse(Console.ReadLine(), out int idA8)) { Console.WriteLine("Id invalido."); Pause(); break; }
                        var usu8 = cadastro.PesquisarUsuario(new Usuario(idU8, ""));
                        var amb8 = cadastro.PesquisarAmbiente(new Ambiente(idA8, ""));
                        if (usu8 == null || amb8 == null) { Console.WriteLine("Usuario ou ambiente nao encontrados."); Pause(); break; }
                        bool rev = usu8.RevogarPermissao(amb8);
                        Console.WriteLine(rev ? "Permissao revogada." : "Usuario nao possui permissao para esse ambiente.");
                        Pause();
                        break;

                    case "9":
                        Console.Write("Id do ambiente: ");
                        if (!int.TryParse(Console.ReadLine(), out int idA9)) { Console.WriteLine("Id invalido."); Pause(); break; }
                        Console.Write("Id do usuario: ");
                        if (!int.TryParse(Console.ReadLine(), out int idU9)) { Console.WriteLine("Id invalido."); Pause(); break; }
                        bool autorizado = cadastro.RegistrarAcesso(idA9, idU9);
                        Console.WriteLine(autorizado ? "Acesso AUTORIZADO." : "Acesso NEGADO.");
                        Pause();
                        break;

                    case "10":
                        Console.Write("Id do ambiente: ");
                        if (!int.TryParse(Console.ReadLine(), out int idALogs)) { Console.WriteLine("Id invalido."); Pause(); break; }
                        var ambLogs = cadastro.PesquisarAmbiente(new Ambiente(idALogs, ""));
                        if (ambLogs == null) { Console.WriteLine("Ambiente nao encontrado."); Pause(); break; }
                        Console.WriteLine("Filtrar logs? (1 - todos, 2 - autorizados, 3 - negados): ");
                        var sel = Console.ReadLine();
                        var todos = ambLogs.GetLogs();
                        IEnumerable<Log> filtrados;
                        if (sel == "2") filtrados = todos.Where(l => l.TipoAcesso == true);
                        else if (sel == "3") filtrados = todos.Where(l => l.TipoAcesso == false);
                        else filtrados = todos;
                        Console.WriteLine($"Logs do ambiente [{ambLogs.Id}] {ambLogs.Nome}:");
                        foreach (var l in filtrados)
                        {
                            var usu = cadastro.Usuarios.FirstOrDefault(u => u.Id == l.UsuarioId);
                            string nomeu = usu != null ? usu.Nome : $"UsuarioId:{l.UsuarioId}";
                            string tipo = l.TipoAcesso ? "AUTORIZADO" : "NEGADO";
                            Console.WriteLine($"  {l.DtAcesso:yyyy-MM-dd HH:mm:ss} - {nomeu} - {tipo}");
                        }
                        Pause();
                        break;

                    default:
                        Console.WriteLine("Opcao invalida.");
                        Pause();
                        break;
                }
            }

        }

        static void Pause()
        {
            Console.WriteLine();
            Console.WriteLine("Pressione ENTER para continuar...");
            Console.ReadLine();
        }
    }
}
