using System;
using System.Numerics;

namespace FractalTreeModel
{
    public enum Step { Left, Right }

    // Точная дробь для работы с относительными координатами в пределах от 0 до 1
    public struct BigFraction
    {
        public BigInteger Numerator { get; }
        public BigInteger Denominator { get; }

        public BigFraction(BigInteger numerator, BigInteger denominator)
        {
            if (denominator == 0) throw new DivideByZeroException();
            BigInteger gcd = BigInteger.GreatestCommonDivisor(numerator, denominator);
            Numerator = numerator / gcd;
            Denominator = denominator / gcd;
            if (Denominator < 0) { Numerator = -Numerator; Denominator = -Denominator; }
        }

        public BigFraction Add(BigFraction other) => new BigFraction(Numerator * other.Denominator + other.Numerator * Denominator, Denominator * other.Denominator);
        public BigFraction Subtract(BigFraction other) => new BigFraction(Numerator * other.Denominator - other.Numerator * Denominator, Denominator * other.Denominator);
        public BigFraction Multiply(BigFraction other) => new BigFraction(Numerator * other.Numerator, Denominator * other.Denominator);
        public BigFraction Divide(BigFraction other) => new BigFraction(Numerator * other.Denominator, Denominator * other.Numerator);
        public override string ToString() => Denominator == 1 ? $"{Numerator}" : $"{Numerator}/{Denominator}";
    }

    public struct ExactPoint
    {
        public BigFraction X { get; }
        public BigFraction FractionY { get; } // Истинное Y = FractionY * √3

        public ExactPoint(BigFraction x, BigFraction fractionY) { X = x; FractionY = fractionY; }

        public BigFraction SquareDistanceTo(ExactPoint other)
        {
            BigFraction deltaX = X.Subtract(other.X);
            BigFraction deltaY = FractionY.Subtract(other.FractionY);
            return deltaX.Multiply(deltaX).Add(deltaY.Multiply(deltaY).Multiply(new BigFraction(3, 1)));
        }

        public override string ToString()
        {
            double doubleX = (double)X.Numerator / (double)X.Denominator;
            double doubleY = ((double)FractionY.Numerator / (double)FractionY.Denominator) * Math.Sqrt(3);
            return $"X = {X}\n Y = {FractionY} * √3\n [Десятичное: ({doubleX:F6}, {doubleY:F6})]";
        }
    }

    public class RelativeTreeSystem
    {
        private readonly BigFraction _scale; // Физический размер основания (например, 1 или 0.25)
        private readonly int _depth;         // Максимальная глубина дерева (например, 1024)
        private readonly ExactPoint _nodeA;
        private readonly ExactPoint _nodeB;
        private readonly ExactPoint _nodeC;

        public RelativeTreeSystem(int depth, BigFraction scale)
        {
            _depth = depth;
            _scale = scale;

            // Маяки жестко привязаны к физическому размеру scale (по умолчанию от 0 до 1)
            _nodeA = new ExactPoint(new BigFraction(0, 1), new BigFraction(0, 1));
            _nodeB = new ExactPoint(_scale, new BigFraction(0, 1));
            _nodeC = new ExactPoint(_scale.Multiply(new BigFraction(1, 2)), _scale.Multiply(new BigFraction(1, 2)));
        }

        // Спуск по дереву в относительных координатах (внутри размера scale)
        public (BigFraction dA2, BigFraction dB2, BigFraction dC2) GenerateDistances(Step[] path)
        {
            // Корень находится строго по центру нашего scale-треугольника
            BigFraction currentX = _scale.Multiply(new BigFraction(1, 2));
            BigFraction currentFractionY = _scale.Multiply(new BigFraction(1, 2));
            
            // На первом шаге смещаемся на 1/4 от размера scale
            BigFraction currentStep = _scale.Multiply(new BigFraction(1, 4));

            foreach (var step in path)
            {
                currentFractionY = currentFractionY.Subtract(currentStep);
                if (step == Step.Left) currentX = currentX.Subtract(currentStep);
                else currentX = currentX.Add(currentStep);

                currentStep = currentStep.Multiply(new BigFraction(1, 2));
            }

            ExactPoint targetNode = new ExactPoint(currentX, currentFractionY);
            Console.WriteLine($"[Генератор] Точные относительные координаты на листе бумаги:\n{targetNode}\n");

            return (targetNode.SquareDistanceTo(_nodeA), targetNode.SquareDistanceTo(_nodeB), targetNode.SquareDistanceTo(_nodeC));
        }

        // Трилатерация на основе относительного размера scale
        public ExactPoint FindNode(BigFraction dA2, BigFraction dB2, BigFraction dC2)
        {
            BigFraction l2 = _scale.Multiply(_scale);

            // x = (L² + dA² - dB²) / 2L
            BigFraction xNum = l2.Add(dA2).Subtract(dB2);
            BigFraction xDenom = _scale.Multiply(new BigFraction(2, 1));
            BigFraction x = xNum.Divide(xDenom);

            // y = (L² + dA² - dC² - L*x) / (L * √3)
            BigFraction yNum = l2.Add(dA2).Subtract(dC2).Subtract(_scale.Multiply(x));
            BigFraction yDenom = _scale.Multiply(new BigFraction(3, 1));
            BigFraction fractionY = yNum.Divide(yDenom);

            return new ExactPoint(x, fractionY);
        }

        // Перевод относительных координат в логический ID узла
        public BigInteger GetNodeId(ExactPoint physicalPoint)
        {
            // Приводим точку к единичному масштабу для расчетов
            BigFraction normX = physicalPoint.X.Divide(_scale);
            BigFraction normY = physicalPoint.FractionY.Divide(_scale);

            BigFraction rootY = new BigFraction(1, 2);
            BigFraction heightDropped = rootY.Subtract(normY);

            int level = 0;
            BigFraction currentStepSize = new BigFraction(1, 4);
            BigFraction accumulatedDrop = new BigFraction(0, 1);

            while (accumulatedDrop.Numerator * heightDropped.Denominator < heightDropped.Numerator * accumulatedDrop.Denominator)
            {
                accumulatedDrop = accumulatedDrop.Add(currentStepSize);
                currentStepSize = currentStepSize.Multiply(new BigFraction(1, 2));
                level++;
            }

            if (level == 0) return 1;

            BigFraction distanceBetweenNodes = currentStepSize.Multiply(new BigFraction(4, 1));
            BigFraction minXForLevel = currentStepSize.Multiply(new BigFraction(2, 1));

            BigFraction xOffset = normX.Subtract(minXForLevel);
            BigInteger indexOnLevel = (xOffset.Numerator * distanceBetweenNodes.Denominator) / 
                                      (xOffset.Denominator * distanceBetweenNodes.Numerator);

            return BigInteger.Pow(2, level) + indexOnLevel;
        }
    }

    class Program
    {
        static void Main()
        {
            // Глубина дерева — 1024 уровня.
            int depth = 1024; 
            
            // Задаем физический размер нашего листа бумаги (основание треугольника).
            // Сначала проверим базовый вариант: размер равен 1.
            BigFraction scale = new BigFraction(1, 1024);

            var system = new RelativeTreeSystem(depth, scale);

            // Путь в 1000 шагов
            Step[] path = new Step[1000];
            Random rand = new Random(42); 
            for (int i = 0; i < 1000; i++) path[i] = rand.Next(0, 2) == 0 ? Step.Left : Step.Right;

            Console.WriteLine("=================================================================");
            Console.WriteLine($" ЧИСТАЯ ОТНОСИТЕЛЬНАЯ ГЕОМЕТРИЯ: ЛИСТ РАЗМЕРОМ {scale}");
            Console.WriteLine("=================================================================\n");
            
            var distances = system.GenerateDistances(path);
            
            Console.WriteLine("--- ТРИЛАТЕРАЦИЯ И ДЕКОДИРОВАНИЕ ---");
            ExactPoint restored = system.FindNode(distances.dA2, distances.dB2, distances.dC2);
            BigInteger id = system.GetNodeId(restored);

            Console.WriteLine($"[Трилатерация] Точка восстановлена:\n{restored}\n");
            Console.WriteLine($"[Индексатор] Узел успешно распознан! ID содержит {id.ToString().Length} знаков.");
        }
    }
}
