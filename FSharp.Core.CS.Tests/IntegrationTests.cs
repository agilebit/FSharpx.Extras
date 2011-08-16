﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using Microsoft.FSharp.Collections;
using Microsoft.FSharp.Core;
using NUnit.Framework;
using Errors = Microsoft.FSharp.Collections.FSharpList<string>;

namespace FSharp.Core.CS.Tests {
    [TestFixture]
    public class IntegrationTests {
        int doSomething(int userID, int id) {
            // fetch some other entity, do "stuff"
            return userID + id;
        }

        void setError(string e) {
            Console.WriteLine("Error: {0}", e);
        }

        const string req_userID = "a123";
        const string req_otherID = "b999";

        [Test]
        public void Test1_Imperative() {

            int userID;
            var userID_ok = int.TryParse(req_userID, out userID);
            if (!userID_ok) {
                setError("Invalid User ID");
            } else {
                int id;
                var id_ok = int.TryParse(req_otherID, out id);
                if (!id_ok) {
                    setError("Invalid ID");
                } else {
                    Console.WriteLine(doSomething(userID, id));
                }
            }
        }

        [Test]
        public void Test1_option() {
            var userID = FSharpOption.TryParseInt(req_userID);
            if (!userID.HasValue()) {
                setError("Invalid User ID");
            } else {
                var otherID = FSharpOption.TryParseInt(req_otherID);
                if (!otherID.HasValue()) {
                    setError("Invalid ID");
                } else {
                    Console.WriteLine(doSomething(userID.Value, otherID.Value));
                }
            }
        }


        [Test]
        public void Test1_either() {

            var somethingOrError =
                from userID in FSharpOption.TryParseInt(req_userID).ToFSharpChoice("Invalid User ID")
                from id in FSharpOption.TryParseInt(req_otherID).ToFSharpChoice("Invalid ID")
                select doSomething(userID, id);

            somethingOrError.Match(Console.WriteLine, setError);

        }

        void setErrors(IEnumerable<string> errors) {
            foreach (var e in errors)
                Console.WriteLine("Error: {0}", e);
        }

        [Test]
        public void Test2_imperative() {
            var errors = new List<string>();

            int userID;
            var userID_ok = int.TryParse(req_userID, out userID);
            if (!userID_ok)
                errors.Add("Invalid user ID");

            int id;
            var id_ok = int.TryParse(req_otherID, out id);
            if (!id_ok)
                errors.Add("Invalid ID");

            if (errors.Count > 0)
                setErrors(errors);
            else
                Console.WriteLine(doSomething(userID, id));
        }

        [Test]
        public void Test2_either() {
            var userID = FSharpOption.TryParseInt(req_userID)
                .ToFSharpChoice(FSharpList.New("Invalid User ID"));
            var id = FSharpOption.TryParseInt(req_otherID)
                .ToFSharpChoice(FSharpList.New("Invalid ID"));

            var doSomethingFunc = L.F((int a, int b) => doSomething(a, b));
            var curriedDoSomething = doSomethingFunc.Curry();
            var result = curriedDoSomething.PureValidate()
                .ApV(userID)
                .ApV(id);

            //var result = L.F((int a, int b) => doSomething(a,b))
            //    .Curry().PureValidate()
            //    .ApV(userID)
            //    .ApV(id);

            result.Match(Console.WriteLine, setErrors);
        }

        [Test]
        public void Test2_either_LINQ() {
            var userID = FSharpOption.TryParseInt(req_userID)
                .ToFSharpChoice(FSharpList.New("Invalid User ID"));
            var id = FSharpOption.TryParseInt(req_otherID)
                .ToFSharpChoice(FSharpList.New("Invalid ID"));

            var result =
                from a in userID
                join b in id on 1 equals 1
                select doSomething(a, b);

            result.Match(Console.WriteLine, setErrors);
        }

        public static FSharpChoice<T, Errors> NonNull<T>(T value, string err) where T: class {
            return FSharpChoice.Validator<T>(x => x != null, err)(value);
            //if (value == null)
            //    return FSharpChoice.Error<T>(err);
            //return FSharpChoice.Ok(value);
        }

        public static FSharpChoice<T, Errors> NotEqual<T>(T value, T other, string err) where T: IEquatable<T> {
            var valueNull = Equals(null, value);
            var otherNull = Equals(null, other);
            if (valueNull && otherNull || valueNull != otherNull || value.Equals(other))
                return FSharpChoice.Ok(value);
            return FSharpChoice.Error<T>(err);
        }

        public static FSharpChoice<Address, Errors> ValidateAddress(Address a) {
            return NonNull(a.Postcode, "Post code can't be null").Select(_ => a);
        }

        public static FSharpChoice<T, Errors> GreaterThan<T>(T value, T other, string err) where T: IComparable<T> {
            var valueNull = Equals(null, value);
            var otherNull = Equals(null, other);
            if (valueNull && otherNull || valueNull != otherNull || value.CompareTo(other) > 0)
                return FSharpChoice.Ok(value);
            return FSharpChoice.Error<T>(err);
        }

        public static FSharpChoice<T?, Errors> GreaterThan<T>(T? value, T? other, string err) where T: struct, IComparable<T> {
            if (!value.HasValue && !other.HasValue || value.HasValue != other.HasValue || value.Value.CompareTo(other.Value) > 0)
                return FSharpChoice.Ok(value);
            return FSharpChoice.Error<T?>(err);
        }

        public static FSharpChoice<Order, Errors> ValidateOrder(Order o) {
            return
                from name in NonNull(o.ProductName, "Product name can't be null")
                from cost in GreaterThan(o.Cost, 0, string.Format("Cost for product '{0}' can't be negative", name))
                select o;
        }

        public static FSharpChoice<IEnumerable<Order>, Errors> ValidateOrders(IEnumerable<Order> orders) {
            var zero = ListModule.Empty<Order>().PureValidate();
            var cons = L.F((FSharpList<Order> oo) => L.F((Order o) => oo.Cons(o)));
            var consV = cons.PureValidate();
            var ooo = orders
                .Select(ValidateOrder)
                .Aggregate(zero, (e, c) => consV.ApV(e).ApV(c));
            return ooo.Select(x => (IEnumerable<Order>)x);
        }

        [Test]
        public void Test3() {
            var customer = new Customer {
                Address = new Address {
                    Postcode = "1424",
                },
                Orders = new[] {
                    new Order {
                        ProductName = "Foo",
                        Cost = 5,
                    },
                    new Order {
                        ProductName = "Bar",
                        Cost = -1,
                    },
                    new Order {
                        ProductName = null,
                        Cost = -1,
                    },
                }
            };
            var result =
                from surname in NonNull(customer.Surname, "Surname can't be null")
                join surname2 in NotEqual(customer.Surname, "foo", "Surname can't be foo") on 1 equals 1
                join address in ValidateAddress(customer.Address) on 1 equals 1
                join orders in ValidateOrders(customer.Orders) on 1 equals 1
                select customer;
            result.Match(c => Assert.Fail("Validation should have failed"),
                         Console.WriteLine);
        }
    }

    public static class ValidationLINQ {
        public static FSharpChoice<R, FSharpList<string>> Join<T,I,K,R>(this FSharpChoice<T, FSharpList<string>> c, FSharpChoice<I, FSharpList<string>> inner, Func<T,K> outerKeySelector, Func<I,K> innerKeySelector, Func<T,I,R> resultSelector) {
            var ff = FSharpChoice.PureValidate(new Func<T, Func<I, Tuple<T, I>>>(a => b => Tuple.Create(a, b)));
            return ff.ApV(c).ApV(inner).Select(t => resultSelector(t.Item1, t.Item2));
        }
    }
}