using System;
using System.Collections.Generic;
using Proto;
using Xunit;
using YxArena.Game;

namespace YxArena.Tests
{
    public class ReviewImportTests
    {
        static PlayerData Player()
        {
            var player = new PlayerData();
            player.publicData.uid = "me";
            player.publicData.username = "test";
            player.publicData.characterId = 1000001;
            player.publicData.level = (Level)5;
            var snap = player.publicData.lastRoundData;
            snap.level = (Level)4;
            snap.unlockGrids = 8;
            snap.usedCards.Add(1000004);
            snap.talents.Add(92);
            return player;
        }

        [Fact]
        public void Optional_null_records_import_without_using_post_round_state()
        {
            var player = Player();
            var snap = player.publicData.lastRoundData;
            snap.handCards = null;
            snap.fateStrategies = null;
            snap.talentTempDatas = null;
            snap.permanentBuffTempDatas = null;
            snap.talentDatas = null;
            snap.privateTalentDatas = null;
            snap.talentResonanceData = null;
            snap.usedKeYinCards = null;
            snap.FZJXCareers = null;
            player.privateData.keYinData = null;
            player.publicData.talentTempDatas[92] = 99;
            player.publicData.talents.Add(999);
            var stages = new List<string>();
            var side = ReviewImport.Side(player, stages.Add);
            Assert.Contains(stages, stage => stage.Contains("缺项：") && stage.Contains("privateData.keYinData") && stage.Contains("talentDatas"));
            Assert.Empty(side.Hand);
            Assert.Empty(side.LearnedCards);
            Assert.Empty(side.BottleCards);
            Assert.Equal(0, side.ValueOfTalent(92));
            Assert.DoesNotContain(999, side.ChosenTalents());
            Assert.NotNull(player.privateData.keYinData);
            Assert.NotNull(player.privateData.talentResonanceData);
            Assert.Empty(player.privateData.keYinData.usedCards);
            TalentDetails.WritePublic(side, player.publicData);
            TalentDetails.WritePrivate(side, player.privateData);
        }

        [Theory]
        [InlineData("handCards")]
        [InlineData("fateStrategies")]
        [InlineData("talentTempDatas")]
        [InlineData("permanentBuffTempDatas")]
        [InlineData("talentDatas")]
        [InlineData("privateTalentDatas")]
        [InlineData("talentResonanceData")]
        [InlineData("usedKeYinCards")]
        [InlineData("FZJXCareers")]
        public void Each_optional_snapshot_record_can_be_absent(string field)
        {
            var player = Player();
            typeof(BattlePlayerLastRoundData).GetField(field).SetValue(player.publicData.lastRoundData, null);
            var side = ReviewImport.Side(player);
            Assert.Equal(1000004, side.Board[0]);
            Assert.Equal(92, side.Talents[0]);
        }

        [Fact]
        public void Chengxin_sword_versions_and_branch_counters_are_copied_without_card_evaluation()
        {
            var player = Player();
            var snap = player.publicData.lastRoundData;
            snap.usedCards.Clear();
            snap.usedCards.Add(ChengxinSetup.CardId(4));
            snap.handCards.Add(ChengxinSetup.CardId(5));
            snap.talents.Clear();
            snap.talents.Add(92);
            snap.talents.Add(20093);
            snap.talents.Add(30094);
            snap.talents.Add(30095);
            snap.talents.Add(30096);
            snap.talentTempDatas[92] = 17;
            player.publicData.talentTempDatas[92] = 123;
            var side = ReviewImport.Side(player);
            Assert.Equal(ChengxinSetup.CardId(4), side.Board[0]);
            Assert.Equal(ChengxinSetup.CardId(5), side.Hand[0]);
            Assert.Equal(new[] { 92, 20093, 30094, 30095, 30096 }, side.ChosenTalents());
            Assert.Equal(17, side.ValueOfTalent(92));
            Assert.Equal(4, side.Level);
        }

        [Fact]
        public void Existing_learning_bottle_and_keyin_records_are_preserved()
        {
            var player = Player();
            var snap = player.publicData.lastRoundData;
            var learned = new BattleTalentData();
            learned.commonParams.Add(ChengxinSetup.CardId(5));
            snap.talentDatas[189] = learned;
            var bottle = new BattleTalentData();
            bottle.commonParams.Add(7000031);
            bottle.commonParams.Add(7010031);
            snap.privateTalentDatas[199] = bottle;
            snap.usedKeYinCards.Add(161);
            var side = ReviewImport.Side(player);
            Assert.Single(side.LearnedCards);
            Assert.Equal(ChengxinSetup.SwordBaseId, side.LearnedCards[0]);
            Assert.Equal(new[] { 7000031, 7010031 }, side.BottleCards);
            Assert.Equal(161, player.privateData.keYinData.usedCards[0]);
        }

        [Theory]
        [InlineData(126)]
        [InlineData(10126)]
        [InlineData(20126)]
        public void Chengxin_formation_ids_import_even_when_optional_talent_records_are_null(int cardId)
        {
            var player = Player();
            player.publicData.lastRoundData.usedCards.Clear();
            player.publicData.lastRoundData.usedCards.Add(cardId);
            player.publicData.lastRoundData.talentDatas = null;
            player.publicData.lastRoundData.privateTalentDatas = null;
            player.privateData.keYinData = null;
            Assert.Equal(cardId, ReviewImport.Side(player).Board[0]);
        }

        [Theory]
        [InlineData("usedCards", "场上卡牌")]
        [InlineData("talents", "仙命")]
        public void Required_snapshot_records_report_missing_data_instead_of_inventing_it(string field, string message)
        {
            var player = Player();
            typeof(BattlePlayerLastRoundData).GetField(field).SetValue(player.publicData.lastRoundData, null);
            Assert.Contains(message, Assert.Throws<InvalidOperationException>(() => ReviewImport.Side(player)).Message);
        }

        [Fact]
        public void Progress_reports_the_specific_import_stage()
        {
            var stages = new List<string>();
            ReviewImport.Side(Player(), stages.Add);
            Assert.Contains("同步共鸣、刻印和副职记录", stages);
            Assert.Equal("读取悟剑和玉瓶记录", stages[stages.Count - 1]);
        }

        [Fact]
        public void Null_learning_and_bottle_records_are_also_safe_outside_review_import()
        {
            var player = Player();
            player.publicData.talentDatas = null;
            player.privateData.talentDatas = null;
            var side = new ArenaSide("test", 1000001, 5, 0);
            TalentDetails.Read(side, player.publicData, player.privateData);
            TalentDetails.WritePublic(side, player.publicData);
            TalentDetails.WritePrivate(side, player.privateData);
            Assert.Empty(side.LearnedCards);
            Assert.Equal(new[] { 0, 0 }, player.privateData.talentDatas[199].commonParams);
        }
    }
}
