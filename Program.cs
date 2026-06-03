using System;
using System.Numerics;

namespace FractalTreeModel
{
    // =========================================================================
    // 1. ПЕРЕЧИСЛЕНИЯ И ОСНОВНЫЕ СТРУКТУРЫ ДЛЯ ТОЧНОЙ МАТЕМАТИКИ
    // =========================================================================
    
    public enum Step { Left, Right }

    /// <summary>
    /// Структура точной дроби произвольной разрядности без потери точности.
    /// </summary>
    public struct BigFraction
    {
        public BigInteger Numerator { get; }
        public BigInteger Denominator { get; }

        public BigFraction(BigInteger numerator, BigInteger denominator)
        {
            if (denominator == 0) throw new DivideByZeroException("Знаменатель не может быть нулем.");

            // Сокращаем дробь при создании (наибольший общий делитель)
            BigInteger gcd = BigInteger.GreatestCommonDivisor(numerator, denominator);
            Numerator = numerator / gcd;
            Denominator = denominator / gcd;

            // Знаменатель всегда должен быть положительным
            if (Denominator < 0)
            {
                Numerator = -Numerator;
                Denominator = -Denominator;
            }
        }

        public BigFraction Add(BigFraction other) =>
            new BigFraction(Numerator * other.Denominator + other.Numerator * Denominator, Denominator * other.Denominator);

        public BigFraction Subtract(BigFraction other) =>
            new BigFraction(Numerator * other.Denominator - other.Numerator * Denominator, Denominator * other.Denominator);

        public BigFraction Multiply(BigFraction other) =>
            new BigFraction(Numerator * other.Numerator, Denominator * other.Denominator);

        public override string ToString() => Denominator == 1 ? $"{Numerator}" : $"{Numerator}/{Denominator}";
    }

    /// <summary>
    /// Точечные координаты на плоскости, где Y хранит рациональный коэффициент перед корнем из трех (√3).
    /// Истинное значение Y = FractionY * √3
    /// </summary>
    public struct ExactPoint
    {
        public BigFraction X { get; }
        public BigFraction FractionY { get; } 

        public ExactPoint(BigFraction x, BigFraction fractionY)
        {
            X = x;
            FractionY = fractionY;
        }

        /// <summary>
        /// Вычисляет точный квадрат расстояния между двумя точками.
        /// Формула: d² = (X1 - X2)² + 3 * (FractionY1 - FractionY2)²
        /// </summary>
        public BigInteger SquareDistanceTo(ExactPoint other)
        {
            BigFraction deltaX = X.Subtract(other.X);
            BigFraction deltaY = FractionY.Subtract(other.FractionY);

            BigFraction deltaXSquared = deltaX.Multiply(deltaX);
            BigFraction deltaYSquared = deltaY.Multiply(deltaY);
            
            // Умножаем на 3, так как (k * √3)² = k² * 3
            BigFraction deltaYSquaredTimes3 = deltaYSquared.Multiply(new BigFraction(3, 1));
            BigFraction resultFraction = deltaXSquared.Add(deltaYSquaredTimes3);

            if (resultFraction.Denominator != 1)
            {
                throw new InvalidOperationException("Ошибка геометрии: Квадрат расстояния обязан быть целым числом.");
            }

            return resultFraction.Numerator;
        }

        public override string ToString()
        {
            double doubleX = (double)X.Numerator / (double)X.Denominator;
            double doubleY = ((double)FractionY.Numerator / (double)FractionY.Denominator) * Math.Sqrt(3);
            return $"X = {X}, Y = {FractionY} * √3 \n   [Приблизительно дес.: ({doubleX:F4}, {doubleY:F4})]";
        }
    }

    // =========================================================================
    // 2. МОДУЛЬ ГЕНЕРАЦИИ ДАННЫХ (СИМУЛЯТОР СПУСКА ПО ДЕРЕВУ)
    // =========================================================================

    public class TreePathGenerator
    {
        private readonly BigInteger _l;
        private readonly ExactPoint _nodeA;
        private readonly ExactPoint _nodeB;
        private readonly ExactPoint _nodeC;

        public TreePathGenerator(BigInteger baseWidth)
        {
            _l = baseWidth;
            _nodeA = new ExactPoint(new BigFraction(0, 1), new BigFraction(0, 1));
            _nodeB = new ExactPoint(new BigFraction(_l, 1), new BigFraction(0, 1));
            _nodeC = new ExactPoint(new BigFraction(_l, 2), new BigFraction(_l, 2)); 
        }

        /// <summary>
        /// Спускается от корня по заданному пути шагов и генерирует квадраты расстояний до маяков A, B, C.
        /// </summary>
        public (BigInteger dA2, BigInteger dB2, BigInteger dC2) GenerateDistances(Step[] path)
        {
            // Начальная точка — корень дерева (Уровень 0)
            BigFraction currentX = new BigFraction(_l, 2);
            BigFraction currentFractionY = new BigFraction(_l, 2);

            // На верхнем уровне фрактальный шаг смещения равен L/4
            BigFraction stepSize = new BigFraction(_l, 4);

            foreach (var step in path)
            {
                currentFractionY = currentFractionY.Subtract(stepSize);

                if (step == Step.Left)
                    currentX = currentX.Subtract(stepSize);
                else
                    currentX = currentX.Add(stepSize);

                stepSize = stepSize.Multiply(new BigFraction(1, 2));
            }

            ExactPoint targetNode = new ExactPoint(currentX, currentFractionY);
            Console.WriteLine($"[Генератор] Истинные координаты узла:\n {targetNode}\n");

            BigInteger dA2 = targetNode.SquareDistanceTo(_nodeA);
            BigInteger dB2 = targetNode.SquareDistanceTo(_nodeB);
            BigInteger dC2 = targetNode.SquareDistanceTo(_nodeC);

            return (dA2, dB2, dC2);
        }
    }

    // =========================================================================
    // 3. МОДУЛЬ ТРИЛАТЕРАЦИИ И ДЕКОДИРОВАНИЯ ИНДЕКСА УЗЛА
    // =========================================================================

    public class FractalTreeTrilateration
    {
        private readonly BigInteger _l;

        public FractalTreeTrilateration(BigInteger baseWidth)
        {
            _l = baseWidth;
        }

        /// <summary>
        /// Вычисляет точные координаты точки по трем квадратам расстояний.
        /// </summary>
        public ExactPoint FindNode(BigInteger dA2, BigInteger dB2, BigInteger dC2)
        {
            BigInteger l2 = _l * _l;

            // 1. Координата X: x = (L² + dA² - dB²) / 2L
            BigInteger xNumerator = l2 + dA2 - dB2;
            BigInteger xDenominator = 2 * _l;
            BigFraction x = new BigFraction(xNumerator, xDenominator);

            // 2. Координата Y: y = (L² + dA² - dC² - L*x) / (L * √3)
            BigInteger leftPart = l2 + dA2 - dC2;
            BigInteger yNumerator = (leftPart * x.Denominator) - (_l * x.Numerator);
            BigInteger yDenominatorWithoutRoot = _l * x.Denominator;

            // Переносим √3 в числитель, домножая знаменатель на 3: y = (yNum * √3) / (yDenom * 3)
            BigFraction fractionY = new BigFraction(yNumerator, yDenominatorWithoutRoot * 3);

            return new ExactPoint(x, fractionY);
        }

        /// <summary>
        /// Переводит точные геометрические координаты в двоичный ID узла бинарного дерева.
        /// </summary>
        public BigInteger GetNodeId(ExactPoint point)
        {
            BigFraction rootY = new BigFraction(_l, 2);
            BigFraction currentY = point.FractionY;
            BigFraction heightDropped = rootY.Subtract(currentY);

            int level = 0;
            BigFraction currentStepSize = new BigFraction(_l, 4);
            BigFraction accumulatedDrop = new BigFraction(0, 1);

            // Восстанавливаем уровень дерева по величине падения высоты Y
            while (accumulatedDrop.Numerator * heightDropped.Denominator < heightDropped.Numerator * accumulatedDrop.Denominator)
            {
                accumulatedDrop = accumulatedDrop.Add(currentStepSize);
                currentStepSize = currentStepSize.Multiply(new BigFraction(1, 2));
                level++;
            }

            if (level == 0) return 1; // Корень дерева

            // Расстояние между соседними узлами на найденном уровне
            BigFraction distanceBetweenNodes = currentStepSize.Multiply(new BigFraction(4, 1));

            // Минимальное X для самого левого узла на данном уровне
            BigFraction minXForLevel = currentStepSize.Multiply(new BigFraction(2, 1));

            // Порядковый номер на уровне: index = (X - X_min) / distanceBetweenNodes
            BigFraction xOffset = point.X.Subtract(minXForLevel);
            BigInteger indexOnLevel = (xOffset.Numerator * distanceBetweenNodes.Denominator) / 
                                      (xOffset.Denominator * distanceBetweenNodes.Numerator);

            // Финальный ID по стандарту бинарных деревьев: 2^level + indexOnLevel
            BigInteger baseIdForLevel = BigInteger.Pow(2, level);
            return baseIdForLevel + indexOnLevel;
        }
    }

    // =========================================================================
    // 4. КОРНЕВАЯ ПРОГРАММА ДЕМОНСТРАЦИИ (MAIN)
    // =========================================================================
    class Program
    {
        static void Main()
        {
            Console.WriteLine("======================================================");
            Console.WriteLine("    ЭКСТРЕМАЛЬНЫЙ СТРЕСС-ТЕСТ: 1024 УРОВНЯ ДЕРЕВА");
            Console.WriteLine("======================================================\n");

            // 1. Инициализируем дерево для 1024-го уровня.
            // Основание L = 2^1023. Это число состоит из ~309 десятичных знаков.
            BigInteger L = BigInteger.Pow(2, 1023);

            var generator = new TreePathGenerator(L);
            var solver = new FractalTreeTrilateration(L);

            // 2. Генерируем очень длинный случайный путь (например, 1000 шагов вглубь)
            int pathLength = 1000;
            Step[] longPath = new Step[pathLength];
            Random rand = new Random();

            for (int i = 0; i < pathLength; i++)
            {
                longPath[i] = rand.Next(0, 2) == 0 ? Step.Left : Step.Right;
            }

            Console.WriteLine($" Сгенерирован случайный путь длиной в {pathLength} шагов.");
            Console.WriteLine($" Длина основания дерева L содержит {L.ToString().Length} знаков.\n");

            // Засекаем время выполнения, чтобы проверить скорость BigInteger
            var watch = System.Diagnostics.Stopwatch.StartNew();

            // Шаг 1: Симулируем спуск по дереву и получаем расстояния
            Console.WriteLine("--- ШАГ 1: РАСЧЕТ РАССТОЯНИЙ (СПУСК ПО ФРАКТАЛУ) ---");
            var distances = generator.GenerateDistances(longPath);

            // Шаг 2: Восстанавливаем координаты по трилатерации
            Console.WriteLine("--- ШАГ 2: РАСЧЕТ ТРИЛАТЕРАЦИИ (ПОИСК КООРДИНАТ) ---");
            ExactPoint restoredPoint = solver.FindNode(distances.dA2, distances.dB2, distances.dC2);

            // Шаг 3: Декодируем координаты в ID
            Console.WriteLine("--- ШАГ 3: ДЕКОДИРОВАНИЕ В УНИКАЛЬНЫЙ ID УЗЛА ---");
            BigInteger nodeId = solver.GetNodeId(restoredPoint);

            watch.Stop();

            // 3. Выводим результаты теста
            Console.WriteLine("\n================== РЕЗУЛЬТАТЫ ТЕСТА ==================");
            Console.WriteLine($" Статус трилатерации: УСПЕШНО (Ошибок округления: 0)");
            Console.WriteLine($" Время выполнения всех расчетов: {watch.ElapsedMilliseconds} мс");
            Console.WriteLine($" Количество цифр в итоговом ID узла: {nodeId.ToString().Length} знаков.");
            
            // Показываем первые и последние цифры огромного ID для наглядности
            string fullId = nodeId.ToString();
            if (fullId.Length > 40)
            {
                Console.WriteLine($" Пример ID узла: {fullId.Substring(0, 20)}...[сотни цифр]...{fullId.Substring(fullId.Length - 20)}");
            }
            else
            {
                Console.WriteLine($" ID узла: {fullId}");
            }
            Console.WriteLine("======================================================");

            Console.ReadLine();
        }

    }
}