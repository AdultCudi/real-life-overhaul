using UnityEngine;

namespace RealLifeOverhaul.GameUi
{
    /// <summary>
    /// A page for the left pane's in-game test (Meta/LeftPaneTest): every kind of part the card has, written with the longest
    /// words a language can ask for (long compound words, long names of posts, long sentences), so that a card that overlaps or
    /// cuts anything shows it whatever the app. The words are the test's own, not the player's: they are never shown outside the
    /// test. Costs nothing unless the test is run.
    /// </summary>
    public static class LeftPaneFixture
    {
        /// <summary>One language's worst words.</summary>
        public sealed class Words
        {
            public string Code, Title, Subtitle, Chip, Chip2, Heading, Label, Value, Check, Line, Note, Entry, Detail, Guide;
        }

        /// <summary>The mod's nine languages, each with a long case for every part.</summary>
        public static readonly Words[] Languages =
        {
            new Words
            {
                Code = "en",
                Title = "Chief information security officer and head of technology risk",
                Subtitle = "BlueStone Imports and Wholesale Distribution, at 4 Pier Road, Harbour District",
                Chip = "Take BSc Computer Science",
                Chip2 = "Open to you",
                Heading = "The ladder and what each rung asks for",
                Label = "Satisfaction with the working conditions",
                Value = "Mon, Wed, Fri at 09:00, and on Saturday at 10:00 when the term is full",
                Check = "Promotion after 49 days on the rung below and a recommendation from your manager",
                Line =
                    "Hudson City University has 4 floors with five classrooms each: choose the floor at its " +
                    "door, and each classroom door shows its course.",
                Note =
                    "Exam: 50 of 100 to pass. Homework: 3 pieces. Attend 50% of the classes or you may not " +
                    "sit it. A class can be joined from 30 minutes before it starts until 60 minutes after.",
                Entry = "Senior security engineer",
                Detail =
                    "Asks 55 points; you have 0. Needs: Security practitioner certificate or Network " +
                    "professional certificate or IT support diploma. Promotion after 30 days on the rung " +
                    "below.",
                Guide = "Professional jobs and the ladder of a large company"
            },
            new Words
            {
                Code = "pt",
                Title = "Diretor de segurança da informação e responsável pelo risco tecnológico",
                Subtitle = "BlueStone Importação e Distribuição por Atacado, Rua do Cais 4, Distrito do Porto",
                Chip = "Fazer o bacharelado em Ciência da Computação",
                Chip2 = "Aberto para você",
                Heading = "A escada de carreira e o que cada degrau exige",
                Label = "Satisfação com as condições de trabalho",
                Value = "Seg, qua e sex às 09:00, e aos sábados às 10:00 quando o semestre está cheio",
                Check = "Promoção após 49 dias no degrau anterior e uma recomendação do seu gerente",
                Line =
                    "A Universidade da Cidade de Hudson tem 4 andares com cinco salas de aula cada: escolha o " +
                    "andar na porta e cada porta de sala mostra o seu curso.",
                Note =
                    "Prova: 50 de 100 para passar. Trabalhos: 3 peças. Frequente 50% das aulas ou você não " +
                    "poderá fazer a prova. Uma aula pode ser acessada de 30 minutos antes do início até 60 " +
                    "minutos depois.",
                Entry = "Engenheiro de segurança sênior",
                Detail =
                    "Exige 55 pontos; você tem 0. Precisa de: certificado de profissional de segurança ou " +
                    "certificado de profissional de redes ou diploma de suporte de TI. Promoção após 30 dias " +
                    "no degrau anterior.",
                Guide = "Empregos profissionais e a escada de uma grande empresa"
            },
            new Words
            {
                Code = "zh-cn",
                Title = "首席信息安全官兼技术风险管理部门负责人",
                Subtitle = "蓝石进出口批发贸易有限公司，港口区码头路四号",
                Chip = "报读计算机科学学士课程",
                Chip2 = "对你开放",
                Heading = "晋升阶梯以及每一级的要求",
                Label = "对工作条件的满意程度",
                Value = "周一、周三、周五上午九点，学期报满时周六上午十点",
                Check = "在下一级岗位任满四十九天并获得直属上级推荐后才可晋升",
                Line = "哈德逊城大学共有四层楼，每层五间教室：在楼层门口选择楼层，每间教室的门上都会显示它的课程。",
                Note = "考试：满分一百分，五十分及格。作业：三份。必须出席一半的课程，否则不能参加考试。课程开始前三十分钟至开始后六十分钟内可以加入。",
                Entry = "高级安全工程师",
                Detail = "需要五十五分；你目前有零分。要求：安全从业者证书或网络专业人员证书或信息技术支持文凭。在下一级岗位任满三十天后可晋升。",
                Guide = "专业工作以及大型公司的晋升阶梯"
            },
            new Words
            {
                Code = "de",
                Title = "Leiter der Informationssicherheit und Abteilungsleiter Technologierisikomanagement",
                Subtitle = "Hafen-Import und Großhandelsvertriebsgesellschaft, Hafenstraße 4, Hafenviertel",
                Chip = "Bachelorstudiengang Informatik belegen",
                Chip2 = "Für Sie offen",
                Heading = "Die Karriereleiter und die Voraussetzungen jeder Stufe",
                Label = "Zufriedenheit mit den Arbeitsbedingungen",
                Value = "Mo, Mi, Fr um 09:00 Uhr und samstags um 10:00 Uhr, wenn das Semester ausgebucht ist",
                Check =
                    "Beförderung nach 49 Tagen auf der darunterliegenden Stufe und einer Empfehlung Ihrer " +
                    "Führungskraft",
                Line =
                    "Die Hudson City Universität hat 4 Stockwerke mit jeweils fünf Seminarräumen: Wählen Sie " +
                    "das Stockwerk an der Tür, und jede Seminarraumtür zeigt ihren Kurs.",
                Note =
                    "Prüfung: 50 von 100 Punkten zum Bestehen. Hausaufgaben: 3 Abgaben. Besuchen Sie 50 % der " +
                    "Unterrichtsstunden, sonst dürfen Sie nicht teilnehmen. Eine Stunde kann ab 30 Minuten " +
                    "vor Beginn bis 60 Minuten danach betreten werden.",
                Entry = "Leitender Sicherheitsingenieur für Informationssysteme",
                Detail =
                    "Verlangt 55 Punkte; Sie haben 0. Erforderlich: Zertifikat Sicherheitsexperte oder " +
                    "Zertifikat Netzwerkfachkraft oder Diplom IT-Support. Beförderung nach 30 Tagen auf der " +
                    "darunterliegenden Stufe.",
                Guide = "Berufliche Tätigkeiten und die Karriereleiter großer Unternehmen"
            },
            new Words
            {
                Code = "fr",
                Title =
                    "Directeur de la sécurité des systèmes d'information et responsable du risque " +
                    "technologique",
                Subtitle = "BlueStone Importation et Distribution en gros, 4 rue du Quai, quartier du Port",
                Chip = "Suivre la licence en informatique",
                Chip2 = "Ouvert pour vous",
                Heading = "L'échelle de carrière et ce que demande chaque échelon",
                Label = "Satisfaction concernant les conditions de travail",
                Value = "Lun, mer, ven à 09:00, et le samedi à 10:00 lorsque le trimestre est complet",
                Check =
                    "Promotion après 49 jours à l'échelon précédent et une recommandation de votre " +
                    "responsable",
                Line =
                    "L'université de Hudson City compte 4 étages de cinq salles chacun : choisissez l'étage à " +
                    "sa porte, et la porte de chaque salle indique son cours.",
                Note =
                    "Examen : 50 sur 100 pour réussir. Devoirs : 3 travaux. Assistez à 50 % des cours, sinon " +
                    "vous ne pourrez pas passer l'examen. On peut rejoindre un cours de 30 minutes avant son " +
                    "début jusqu'à 60 minutes après.",
                Entry = "Ingénieur principal en sécurité des systèmes",
                Detail =
                    "Demande 55 points ; vous en avez 0. Exige : certificat de professionnel de la sécurité " +
                    "ou certificat de professionnel des réseaux ou diplôme de support informatique. Promotion " +
                    "après 30 jours à l'échelon précédent.",
                Guide = "Les métiers qualifiés et l'échelle de carrière d'une grande entreprise"
            },
            new Words
            {
                Code = "es",
                Title = "Director de seguridad de la información y responsable del riesgo tecnológico",
                Subtitle =
                    "BlueStone Importaciones y Distribución Mayorista, calle del Muelle 4, distrito del " +
                    "Puerto",
                Chip = "Cursar el grado en Informática",
                Chip2 = "Abierto para ti",
                Heading = "La escalera profesional y lo que exige cada peldaño",
                Label = "Satisfacción con las condiciones de trabajo",
                Value = "Lun, mié y vie a las 09:00, y los sábados a las 10:00 cuando el trimestre está completo",
                Check = "Ascenso tras 49 días en el peldaño anterior y una recomendación de tu responsable",
                Line =
                    "La Universidad de Hudson City tiene 4 plantas con cinco aulas cada una: elige la planta " +
                    "en su puerta, y la puerta de cada aula muestra su curso.",
                Note =
                    "Examen: 50 de 100 para aprobar. Tareas: 3 trabajos. Asiste al 50 % de las clases o no " +
                    "podrás presentarte. Se puede entrar en una clase desde 30 minutos antes de que empiece " +
                    "hasta 60 minutos después.",
                Entry = "Ingeniero principal de seguridad de sistemas",
                Detail =
                    "Pide 55 puntos; tienes 0. Requiere: certificado de profesional de seguridad o " +
                    "certificado de profesional de redes o diploma de soporte informático. Ascenso tras 30 " +
                    "días en el peldaño anterior.",
                Guide = "Empleos profesionales y la escalera de una gran empresa"
            },
            new Words
            {
                Code = "ru",
                Title = "Директор по информационной безопасности и руководитель отдела технологических рисков",
                Subtitle = "Портовая импортно-оптовая торговая компания, Портовая улица, дом 4, портовый район",
                Chip = "Пройти бакалавриат по информатике",
                Chip2 = "Доступно вам",
                Heading = "Карьерная лестница и требования каждой ступени",
                Label = "Удовлетворённость условиями работы",
                Value = "Пн, ср, пт в 09:00 и по субботам в 10:00, если семестр заполнен",
                Check = "Повышение после 49 дней на предыдущей ступени и рекомендация вашего руководителя",
                Line =
                    "В университете Гудзон-Сити 4 этажа по пять аудиторий на каждом: выберите этаж у входа, и " +
                    "на двери каждой аудитории указан её курс.",
                Note =
                    "Экзамен: для сдачи нужно набрать 50 из 100. Домашние задания: 3 работы. Посетите 50% " +
                    "занятий, иначе вас не допустят к экзамену. На занятие можно зайти за 30 минут до начала " +
                    "и до 60 минут после него.",
                Entry = "Ведущий инженер по информационной безопасности",
                Detail =
                    "Требует 55 очков; у вас 0. Нужно: сертификат специалиста по безопасности или сертификат " +
                    "сетевого специалиста или диплом техподдержки. Повышение после 30 дней на предыдущей " +
                    "ступени.",
                Guide = "Профессиональная работа и карьерная лестница крупной компании"
            },
            new Words
            {
                Code = "nl",
                Title = "Hoofd informatiebeveiliging en verantwoordelijke voor technologische risico's",
                Subtitle = "BlueStone Import en Groothandelsdistributie, Kaaistraat 4, havenwijk",
                Chip = "Bachelor Informatica volgen",
                Chip2 = "Open voor jou",
                Heading = "De carrièreladder en wat elke sport vraagt",
                Label = "Tevredenheid over de arbeidsomstandigheden",
                Value = "Ma, wo, vr om 09:00 en op zaterdag om 10:00 als het semester vol zit",
                Check = "Promotie na 49 dagen op de sport eronder en een aanbeveling van je leidinggevende",
                Line =
                    "De Hudson City Universiteit heeft 4 verdiepingen met elk vijf lokalen: kies de " +
                    "verdieping bij de deur en elke lokaaldeur toont zijn cursus.",
                Note =
                    "Examen: 50 van de 100 punten om te slagen. Huiswerk: 3 opdrachten. Volg 50% van de " +
                    "lessen, anders mag je niet deelnemen. Je kunt een les betreden vanaf 30 minuten voor de " +
                    "start tot 60 minuten erna.",
                Entry = "Senior beveiligingsingenieur informatiesystemen",
                Detail =
                    "Vraagt 55 punten; je hebt er 0. Vereist: certificaat beveiligingsprofessional of " +
                    "certificaat netwerkprofessional of diploma IT-ondersteuning. Promotie na 30 dagen op de " +
                    "sport eronder.",
                Guide = "Professionele banen en de carrièreladder van een groot bedrijf"
            },
            new Words
            {
                Code = "tr",
                Title = "Bilgi güvenliği direktörü ve teknoloji riskleri bölümü sorumlusu",
                Subtitle = "BlueStone İthalat ve Toptan Dağıtım Şirketi, İskele Caddesi 4, Liman Bölgesi",
                Chip = "Bilgisayar Bilimleri lisansına başla",
                Chip2 = "Size açık",
                Heading = "Kariyer merdiveni ve her basamağın istedikleri",
                Label = "Çalışma koşullarından memnuniyet",
                Value = "Pzt, çar, cum 09:00'da ve dönem doluysa cumartesi 10:00'da",
                Check = "Bir alt basamakta 49 gün çalıştıktan sonra ve yöneticinizin tavsiyesiyle terfi",
                Line =
                    "Hudson City Üniversitesi'nin 4 katı var ve her katta beş derslik bulunuyor: katı " +
                    "kapısında seçin, her dersliğin kapısında dersi yazıyor.",
                Note =
                    "Sınav: geçmek için 100 üzerinden 50. Ödev: 3 parça. Derslerin yüzde 50'sine katılın, " +
                    "yoksa sınava giremezsiniz. Bir derse başlamadan 30 dakika öncesinden 60 dakika sonrasına " +
                    "kadar katılabilirsiniz.",
                Entry = "Kıdemli bilgi sistemleri güvenlik mühendisi",
                Detail =
                    "55 puan ister; sizde 0 var. Gerekenler: güvenlik uzmanı sertifikası veya ağ uzmanı " +
                    "sertifikası veya BT destek diploması. Bir alt basamakta 30 gün çalıştıktan sonra terfi.",
                Guide = "Profesyonel işler ve büyük bir şirketin kariyer merdiveni"
            }
        };

        /// <summary>
        /// Writes a page of every part in one language's worst words: a header with two chips, two sections of facts (one long label,
        /// one long value, a pair, a meter, needs), a section of list entries, a selector and three buttons.
        /// </summary>
        public static void Write(InfoBox box, Words w, bool buttons)
        {
            box.Clear();
            box.Title(w.Title);
            box.Subtitle(w.Subtitle);
            box.Chip(w.Chip, CellTone.Plain);
            box.Chip(w.Chip2, CellTone.Green);
            box.Line(w.Line);
            box.SectionWords(w.Heading);
            box.RowWords(w.Label, w.Value);
            box.RowWords("Pay", "$120.00");
            box.RowWords(w.Label, "$120.00", CellTone.Yellow);
            box.RowWords("Mode", w.Value, CellTone.Red);
            box.RowPairWords("Pay", "$120.00", "Hours", "40");
            box.RowPairWords(w.Label, w.Value, w.Label, w.Value);
            box.MeterWords(w.Label, 0.6f, w.Value, CellTone.Plain);
            box.Check(w.Check, true);
            box.Check(w.Check, false);
            box.Note(w.Note);
            box.SectionWords(w.Heading);
            for (int i = 0; i < 4; i++)
            {
                box.Entry(w.Entry, "$" + (120 + i * 75) + ".00", w.Detail, i == 2 ? CellTone.Green : CellTone.Plain, i == 2);
            }
            box.Selector(3, 1, i => w.Label, i => { });
            box.Note(w.Note);
            if (buttons)
            {
                Transform row = box.ButtonRow();
                InfoBox.Button(row, InfoBox.Positive, "reallife_campus_button_enrol", () => { });
                InfoBox.Button(row, InfoBox.Additional, "reallife_campus_button_loan", () => { });
                InfoBox.Button(row, InfoBox.Additional, "common_close", () => { });
            }
            box.GuideLink(w.Guide);
            box.Done();
        }
    }
}
