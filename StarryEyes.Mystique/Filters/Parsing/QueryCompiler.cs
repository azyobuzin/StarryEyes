﻿using System;
using System.Collections.Generic;
using System.Linq;
using StarryEyes.Mystique.Filters.Expressions;
using StarryEyes.Mystique.Filters.Expressions.Operators;
using StarryEyes.Mystique.Filters.Expressions.Values;
using StarryEyes.Mystique.Filters.Expressions.Values.Immediates;
using StarryEyes.Mystique.Filters.Expressions.Values.Locals;
using StarryEyes.Mystique.Filters.Expressions.Values.Statuses;
using StarryEyes.Mystique.Filters.Expressions.Values.Users;
using StarryEyes.Mystique.Filters.Sources;

namespace StarryEyes.Mystique.Filters.Parsing
{
    public static class QueryCompiler
    {
        public static FilterQuery Compile(string query)
        {
            try
            {
                var tokens = Tokenizer.Tokenize(query);
                // from (sources) where (filters)
                var first = tokens.FirstOrDefault();
                if (first.Type != TokenType.Literal ||
                    !first.Value.Equals("from", StringComparison.CurrentCultureIgnoreCase))
                    throw new FormatException("Query must be started with \"from\" keyword.");
                var sources = CompileSources(tokens.Skip(1).TakeWhile(t => t.Type != TokenType.Literal || t.Value != "where")).ToArray();
                var filters = CompileFilters(tokens.Skip(1).SkipWhile(t => t.Type != TokenType.Literal || t.Value != "where").Skip(1));
                return new FilterQuery() { Sources = sources, PredicateTreeRoot = filters };
            }
            catch (FilterQueryException)
            {
                throw;
            }
            catch (Exception ex)
            {
                throw new FilterQueryException("Query compilation failed. " + ex.Message, query, ex);
            }
        }

        public static FilterExpressionRoot CompileFilters(string query)
        {
            try
            {
                var tokens = Tokenizer.Tokenize(query);
                // from (sources) where (filters)
                return CompileFilters(tokens);
            }
            catch (FilterQueryException)
            {
                throw;
            }
            catch (Exception ex)
            {
                throw new FilterQueryException("Query compilation failed. " + ex.Message, query, ex);
            }
        }

        #region sources compiler

        private static readonly IDictionary<string, Type> FilterSourceResolver = new SortedDictionary<string, Type>()
        {
            { "local", typeof(FilterLocal) },
            { "all", typeof(FilterLocal) },
            { "home", typeof(FilterHome) },
            { "list", typeof(FilterList) },
            { "mention", typeof(FilterMentions) },
            { "mentions", typeof(FilterMentions) },
            { "reply", typeof(FilterMentions) },
            { "replies", typeof(FilterMentions) },
            { "message", typeof(FilterMessages) },
            { "messages", typeof(FilterMessages) },
            { "dm", typeof(FilterMessages) },
            { "dms", typeof(FilterMessages) },
            { "search", typeof(FilterSearch) },
            { "find", typeof(FilterSearch) },
            { "track", typeof(FilterTrack) },
            { "stream", typeof(FilterTrack) },
        };

        /// <summary>
        /// Instantiate sources from tokens.
        /// </summary>
        private static IEnumerable<FilterSourceBase> CompileSources(IEnumerable<Token> token)
        {
            // filter
            // filter: "argument"
            // filter: "argument1", "argument2", ... -> filter: "argument1", filter: "argument2", ...
            var reader = new TokenReader(token);
            while (reader.IsRemainToken)
            {
                var filter = reader.AssertGet(TokenType.Literal);
                Type fstype;
                if (!FilterSourceResolver.TryGetValue(filter.Value, out fstype))
                    throw new ArgumentException("Unexpected filter type: " + filter.Value);
                if (reader.IsRemainToken && reader.LookAhead().Type == TokenType.Collon) // with argument
                {
                    reader.AssertGet(TokenType.Collon);
                    do
                    {
                        var argument = reader.AssertGet(TokenType.String);
                        yield return Activator.CreateInstance(fstype, argument.Value) as FilterSourceBase;
                        // separated by comma
                        if (reader.IsRemainToken)
                        {
                            reader.AssertGet(TokenType.Comma);
                        }
                    } while (reader.IsRemainToken && reader.LookAhead().Type == TokenType.String);
                }
                else
                {
                    yield return Activator.CreateInstance(fstype) as FilterSourceBase;
                    if (reader.IsRemainToken)
                    {
                        // filters are divided by comma
                        reader.AssertGet(TokenType.Comma);
                    }
                }
            }
        }

        #endregion

        #region filter expression tree compiler

        /// <summary>
        /// Instantiate expression tree from tokens.
        /// </summary>
        private static FilterExpressionRoot CompileFilters(IEnumerable<Token> token)
        {
            var reader = new TokenReader(token);
            var op = CompileL0(reader);
            if (reader.IsRemainToken)
                throw new FilterQueryException("Invalid token: " + reader.Get(), reader.RemainQuery);
            return new FilterExpressionRoot() { Operator = op };
        }

        // Operators:
        // All: + - * / || && <- -> == > >= < <= != ! 
        // L0: ||
        // L1: &&
        // L2: == !=
        // L3: < <= > >=
        // L4: <- ->
        // L5: + -
        // L6: * /
        // L7: !
        // L8: value (or in bracket, return L0)

        private static FilterOperatorBase CompileL0(TokenReader reader)
        {
            // ||
            var left = CompileL1(reader);
            if (!reader.IsRemainToken)
                return left;
            var generate = (Func<TokenType, FilterTwoValueOperator, FilterOperatorBase>)
                ((type, oper) => GenerateSink(reader, left, type, oper, CompileL0));
            switch (reader.LookAhead().Type)
            {
                case TokenType.OperatorOr:
                    return generate(TokenType.OperatorOr, new FilterOperatorOr());
                default:
                    return left;
            }
        }

        private static FilterOperatorBase CompileL1(TokenReader reader)
        {
            // &&
            var left = CompileL2(reader);
            if (!reader.IsRemainToken)
                return left;
            var generate = (Func<TokenType, FilterTwoValueOperator, FilterOperatorBase>)
                ((type, oper) => GenerateSink(reader, left, type, oper, CompileL1));
            switch (reader.LookAhead().Type)
            {
                case TokenType.OperatorAnd:
                    return generate(TokenType.OperatorAnd, new FilterOperatorAnd());
                default:
                    return left;
            }
        }

        private static FilterOperatorBase CompileL2(TokenReader reader)
        {
            // == !=
            var left = CompileL3(reader);
            if (!reader.IsRemainToken)
                return left;
            var generate = (Func<TokenType, FilterTwoValueOperator, FilterOperatorBase>)
                ((type, oper) => GenerateSink(reader, left, type, oper, CompileL2));
            switch (reader.LookAhead().Type)
            {
                case TokenType.OperatorEquals:
                    return generate(TokenType.OperatorEquals, new FilterOperatorEquals());
                case TokenType.OperatorNotEquals:
                    return generate(TokenType.OperatorNotEquals, new FilterOperatorNotEquals());
                default:
                    return left;
            }
        }

        private static FilterOperatorBase CompileL3(TokenReader reader)
        {
            // < <= > >=
            var left = CompileL4(reader);
            if (!reader.IsRemainToken)
                return left;
            var generate = (Func<TokenType, FilterTwoValueOperator, FilterOperatorBase>)
                ((type, oper) => GenerateSink(reader, left, type, oper, CompileL3));
            switch (reader.LookAhead().Type)
            {
                case TokenType.OperatorLessThan:
                    return generate(TokenType.OperatorLessThan, new FilterOperatorLessThan());
                case TokenType.OperatorLessThanOrEqual:
                    return generate(TokenType.OperatorLessThanOrEqual, new FilterOperatorLessThanOrEqual());
                case TokenType.OperatorGreaterThan:
                    return generate(TokenType.OperatorGreaterThan, new FilterOperatorGreaterThan());
                case TokenType.OperatorGreaterThanOrEqual:
                    return generate(TokenType.OperatorGreaterThanOrEqual, new FilterOperatorGreaterThanOrEqual());
                default:
                    return left;
            }
        }

        private static FilterOperatorBase CompileL4(TokenReader reader)
        {
            // <- ->
            var left = CompileL5(reader);
            if (!reader.IsRemainToken)
                return left;
            var generate = (Func<TokenType, FilterTwoValueOperator, FilterOperatorBase>)
                ((type, oper) => GenerateSink(reader, left, type, oper, CompileL4));
            switch (reader.LookAhead().Type)
            {
                case TokenType.OperatorContains:
                    return generate(TokenType.OperatorContains, new FilterOperatorContains());
                case TokenType.OperatorContainedBy:
                    return generate(TokenType.OperatorContainedBy, new FilterOperatorContainedBy());
                default:
                    return left;
            }
        }

        private static FilterOperatorBase CompileL5(TokenReader reader)
        {
            // parse arithmetic operators
            var left = CompileL6(reader);
            if (!reader.IsRemainToken)
                return left;
            var generate = (Func<TokenType, FilterTwoValueOperator, FilterOperatorBase>)
                ((type, oper) => GenerateSink(reader, left, type, oper, CompileL5));
            switch (reader.LookAhead().Type)
            {
                case TokenType.OperatorPlus:
                    return generate(TokenType.OperatorPlus, new FilterOperatorPlus());
                case TokenType.OperatorMinus:
                    return generate(TokenType.OperatorMinus, new FilterOperatorMinus());
                default:
                    return left;
            }
        }

        private static FilterOperatorBase CompileL6(TokenReader reader)
        {
            // parse arithmetic operators (faster)
            var left = CompileL7(reader);
            if (!reader.IsRemainToken)
                return left;
            var generate = (Func<TokenType, FilterTwoValueOperator, FilterOperatorBase>)
                ((type, oper) => GenerateSink(reader, left, type, oper, CompileL6));
            switch (reader.LookAhead().Type)
            {
                case TokenType.OperatorMultiple:
                    return generate(TokenType.OperatorMultiple, new FilterOperatorProduct());
                case TokenType.OperatorDivide:
                    return generate(TokenType.OperatorDivide, new FilterOperatorDivide());
                default:
                    return left;
            }
        }

        private static FilterOperatorBase CompileL7(TokenReader reader)
        {
            // parse not 
            if (reader.LookAhead().Type == TokenType.Exclamation)
            {
                reader.AssertGet(TokenType.Exclamation);
                return new FilterNegate() { Value = CompileL7(reader) };
            }
            else
            {
                return CompileL8(reader);
            }
        }

        private static FilterOperatorBase CompileL8(TokenReader reader)
        {
            if (reader.LookAhead().Type == TokenType.OpenBracket)
            {
                // in bracket
                reader.AssertGet(TokenType.OpenBracket);
                var ret = CompileL0(reader);
                reader.AssertGet(TokenType.CloseBracket);
                return new FilterBracket(ret);
            }
            else
            {
                return InstantiateValue(reader);
            }
        }

        private static FilterOperatorBase GenerateSink(
            TokenReader reader,
            FilterOperatorBase leftValue,
            TokenType type,
            FilterTwoValueOperator oper,
            Func<TokenReader, FilterOperatorBase> selfCall)
        {
            reader.AssertGet(type);
            var rightValue = selfCall(reader);
            oper.LeftValue = leftValue;
            oper.RightValue = rightValue;
            return oper;
        }

        private static ValueBase InstantiateValue(TokenReader reader)
        {
            var literal = reader.LookAhead();
            if (literal.Type == TokenType.String)
            {
                // immediate string value
                return new StringValue(reader.AssertGet(TokenType.String).Value);
            }
            else if (literal.Type == TokenType.OperatorMultiple)
            {
                // for parsing asterisk user
                var _pseudo = reader.AssertGet(TokenType.OperatorMultiple);
                literal = new Token(TokenType.Literal, "*", _pseudo.DebugIndex);
            }
            else
            {
                literal = reader.AssertGet(TokenType.Literal);
            }
            // check first layers
            switch (literal.Value)
            {
                case "*":
                    return InstantiateLocalUsers("*", reader);
                case "local":
                    reader.AssertGet(TokenType.Period);
                    return InstantiateLocalUsers(reader.AssertGet(TokenType.Literal).Value, reader);
                case "user":
                case "retweeter":
                    return InstantiateUserValue(literal.Value == "retweeter", reader);
                default:
                    long iv = 0;
                    if (Int64.TryParse(literal.Value, out iv))
                    {
                        return new NumericValue(iv);
                    }
                    else
                    {
                        return InstantiateStatusValue(literal.Value, reader);
                    }
            }
        }

        private static ValueBase InstantiateLocalUsers(string value, TokenReader reader)
        {
            var repl = GetRepresentation(value);
            if (reader.IsRemainToken && reader.LookAhead().Type == TokenType.Period)
            {
                reader.AssertGet(TokenType.Period);
                var literal = reader.AssertGet(TokenType.Literal);
                switch (literal.Value)
                {
                    case "friend":
                    case "friends":
                    case "following":
                    case "followings":
                        return new LocalUserFollowings(repl);
                    case "follower":
                    case "followers":
                        return new LocalUserFollowers(repl);
                    default:
                        throw new FilterQueryException("Unexpected token: " + literal.Value, repl.ToQuery() + "." + literal.Value + " " + reader.RemainQuery);
                }
            }
            else
            {
                return new LocalUser(repl);
            }
        }

        private static UserRepresentationBase GetRepresentation(string key)
        {
            if (key == "*")
            {
                return new UserAny();
            }
            else if (key.StartsWith("#"))
            {
                var id = Int64.Parse(key.Substring(1));
                return new UserSpecified(id);
            }
            else
            {
                return new UserSpecified(key);
            }
        }

        private static ValueBase InstantiateUserValue(bool isRetweeter, TokenReader reader)
        {
            var selector = (Func<ValueBase, ValueBase, ValueBase>)
                ((user, retweeter) => isRetweeter ? retweeter : user);
            if (reader.IsRemainToken && reader.LookAhead().Type != TokenType.Period)
            {
                // user representation
                return selector(new User(), new Retweeter());
            }
            reader.AssertGet(TokenType.Period);
            var literal = reader.AssertGet(TokenType.Literal);
            switch (literal.Value)
            {
                case "protected":
                case "isProtected":
                case "is_protected":
                    return selector(new UserIsProtected(), new RetweeterIsProtected());
                case "verified":
                case "isVerified":
                case "is_verified":
                    return selector(new UserIsVerified(), new RetweeterIsVerified());
                case "translator":
                case "isTranslator":
                case "is_translator":
                    return selector(new UserIsTranslator(), new RetweeterIsTranslator());
                case "contributorsEnabled":
                case "contributors_enabled":
                case "isContributorsEnabled":
                case "is_contributors_enabled":
                    return selector(new UserIsContributorsEnabled(), new RetweeterIsContributorsEnabled());
                case "geoEnabled":
                case "geo_enabled":
                case "isGeoEnabled":
                case "is_geo_enabled":
                    return selector(new UserIsGeoEnabled(), new RetweeterIsGeoEnabled());
                case "id":
                    return selector(new UserId(), new RetweeterId());
                case "status":
                case "statuses":
                case "statusCount":
                case "status_count":
                case "statusesCount":
                case "statuses_count":
                    return selector(new UserStatuses(), new RetweeterStatuses());
                case "friend":
                case "friends":
                case "following":
                case "followings":
                case "friendsCount":
                case "friends_count":
                case "followingsCount":
                case "followings_count":
                    return selector(new UserFriends(), new RetweeterFriends());
                case "follower":
                case "followers":
                case "followersCount":
                case "followers_count":
                    return selector(new UserFollowers(), new RetweeterFollowers());
                case "fav":
                case "favCount":
                case "favorite":
                case "favorites":
                case "fav_count":
                case "favoriteCount":
                case "favorite_count":
                case "favoritesCount":
                case "favorites_count":
                    return selector(new UserFavroites(), new RetweeterFavroites());
                case "list":
                case "listed":
                case "listCount":
                case "list_count":
                case "listedCount":
                case "listed_count":
                    return selector(new UserListed(), new RetweeterListed());
                case "screenName":
                case "screen_name":
                    return selector(new UserScreenName(), new RetweeterScreenName());
                case "name":
                    return selector(new UserName(), new RetweeterName());
                case "bio":
                case "desc":
                case "description":
                    return selector(new UserDescription(), new RetweeterDescription());
                case "loc":
                case "location":
                    return selector(new UserLocation(), new RetweeterLocation());
                case "lang":
                case "language":
                    return selector(new UserLanguage(), new RetweeterLanguage());
                default:
                    throw new FilterQueryException("Unexpected token: " + literal.Value, "user." + literal.Value + " " + reader.RemainQuery);
            }
        }

        private static ValueBase InstantiateStatusValue(string value, TokenReader reader)
        {
            switch (value)
            {
                case "dm":
                case "isDm":
                case "is_dm":
                case "isMessage":
                case "is_message":
                case "isDirectMessage":
                case "is_direct_message":
                    return new StatusIsDirectMessage();
                case "retweet":
                case "isRetweet":
                case "is_retweet":
                    return new StatusIsRetweet();
                case "favorited":
                case "isFavorited":
                case "is_favorited":
                    return new StatusIsFavorited();
                case "retweeted":
                case "isRetweeted":
                case "is_retweeted":
                    return new StatusIsRetweeted();
                case"replyTo":
                case "reply_to":
                case "inReplyTo":
                case "in_reply_to":
                    return new StatusInReplyTo();
                case "to":
                    return new StatusTo();
                case "id":
                    return new StatusId();
                case "favorer":
                case "favorers":
                    return new StatusFavorers();
                case "retweeter":
                case "retweeters":
                    return new StatusRetweeters();
                case "text":
                case "body":
                    return new StatusText();
                case "via":
                case "from":
                case "source":
                case "client":
                    return new StatusSource();
                default:
                    throw new FilterQueryException("Unexpected token: " + value, value + " " + reader.RemainQuery);
            }
        }

        #endregion
    }
}
